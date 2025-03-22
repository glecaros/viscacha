using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.TestHost;
using Viscacha.Model;
using Viscacha.Model.Test;
using Viscacha.TestRunner.Framework.Validation;
using YAYL;

namespace Viscacha.TestRunner.Framework;

internal record FrameworkTestVariant(string Name, Document Request, bool Baseline);
internal record FrameworkTest(string Name, List<FrameworkTestVariant> Variants, List<ValidationDefinition> Validations);
internal record TestVariantResult(FrameworkTestVariant Variant, List<ResponseWrapper> Responses);

internal sealed class Session(SessionUid uid)
{
    public SessionUid Uid { get; } = uid;

    private bool _failed = false;
    private bool _initialized = false;
    private string? _path;
    private List<FrameworkTest> _tests = [];

    private async Task<Result<Error>> InitAsyncInternal(string path, CancellationToken cancellationToken)
    {
        var pathInfo = new FileInfo(path);
        _path = pathInfo.FullName;
        var suiteFileDirectory = pathInfo.DirectoryName ?? string.Empty;
        var parser = new YamlParser();
        try {
            var suite = await parser.ParseFileAsync<Suite>(path, cancellationToken).ConfigureAwait(false);
            if (suite is null)
            {
                return new Error($"Failed to parse suite file: {path}");
            }
            Dictionary<string, (FileInfo File, bool Baseline)> configurations = [];
            foreach (var configuration in suite.Configurations)
            {
                var filePath = Path.Combine(suiteFileDirectory, configuration.Path);
                if (!File.Exists(filePath))
                {
                    return new Error($"File for configuration {configuration.Name} not found: {filePath}");
                }
                configurations[configuration.Name] = (new FileInfo(filePath), configuration.Baseline ?? false);
            }
            List<FrameworkTest> tests = [];
            foreach (var test in suite.Tests)
            {
                var variables = suite.Variables.Merge(test.Variables) ?? [];
                var documentParser = new DocumentParser(variables);
                var requestFilePath = Path.Combine(suiteFileDirectory, test.RequestFile);
                FileInfo? testFile;
                if (File.Exists(requestFilePath))
                {
                    testFile = new FileInfo(requestFilePath);
                }
                else
                {
                    return new Error($"File for test {test.Name} not found: {test.RequestFile}");
                }

                List<FrameworkTestVariant> testVariants = [];
                foreach (var variant in test.Configurations)
                {
                    if (!configurations.TryGetValue(variant, out var configuration))
                    {
                        return new Error($"Configuration {variant} required by test {test.Name} not found");
                    }
                    switch (await documentParser.FromFileAsync(testFile, configuration.File,  cancellationToken).ConfigureAwait(false))
                    {
                        case Result<Document, Error>.Err error:
                            return error.Error;
                        case Result<Document, Error>.Ok document:
                            testVariants.Add(new FrameworkTestVariant(variant, document.Value, configuration.Baseline));

                            break;
                    }
                }
                tests.Add(new FrameworkTest(test.Name, testVariants, test.Validations));
            }
            _initialized = true;
            _tests = tests;
            return new Result<Error>.Ok();

        }
        catch (YamlParseException e)
        {
            return new Error(e.Message);
        }
    }

    public async Task<Result<Error>> InitAsync(string path, CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return new Error("Session already initialized.");
        }
        if (_failed)
        {
            return new Error("Session failed to initialize.");
        }
        return (await InitAsyncInternal(path, cancellationToken).ConfigureAwait(false)).Else<Error>(e =>
        {
            _failed = true;
            return e;
        });
    }

    private string GetStableUid(FrameworkTest test)
    {
        return $"{_path}.{test.Name}";
    }

    public async Task DiscoverTestsAsync(IDataProducer producer, ExecuteRequestContext context, CancellationToken _)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Session not initialized.");
        }
        foreach (var test in _tests)
        {
            var testNode = new TestNode
            {
                Uid = GetStableUid(test),
                DisplayName = test.Name,
                Properties = new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance),
            };
            await context.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(Uid, testNode)).ConfigureAwait(false);
        }
    }

    private async Task<Result<List<ResponseWrapper>, Error>> ExecuteVariantAsync(HttpClient client, FrameworkTestVariant variant, CancellationToken cancellationToken)
    {
        var executor = new RequestExecutor(variant.Request.Defaults);
        var responses = new List<ResponseWrapper>();
        foreach (var request in variant.Request.Requests)
        {
            var requestIndex = variant.Request.Requests.IndexOf(request);
            var result = await executor.ExecuteAsync(client, request, requestIndex, cancellationToken);
            if (result is Result<ResponseWrapper, Error>.Err { Error: { } error })
            {
                return new Error($"Error executing request index {requestIndex} of variant {variant.Name}: {error.Message}");
            }
            var response = result.Unwrap();
            if (response is null)
            {
                return new Error($"Response is null for request index {requestIndex} of variant {variant.Name}");
            }
            responses.Add(response);
        }
        return responses;
    }

    public async Task RunTestsAsync(IDataProducer producer, ExecuteRequestContext context, CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Session not initialized.");
        }
        using HttpClient client = new();
        foreach (var test in _tests)
        {
            var testNode = new TestNode
            {
                Uid = GetStableUid(test),
                DisplayName = test.Name,
                Properties = new PropertyBag(InProgressTestNodeStateProperty.CachedInstance),
            };
            await context.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(Uid, testNode)).ConfigureAwait(false);
            List<TestVariantResult> testResults = [];
            foreach (var variant in test.Variants)
            {
                var result = await ExecuteVariantAsync(client, variant, cancellationToken).ConfigureAwait(false);
                if (result is Result<List<ResponseWrapper>, Error>.Err { Error: { } error })
                {
                    var errorNode = new TestNode
                    {
                        Uid = GetStableUid(test),
                        DisplayName = test.Name,
                        Properties = new(new FailedTestNodeStateProperty(error.Message)),
                    };
                    await context.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(Uid, errorNode)).ConfigureAwait(false);
                    continue;
                }
                var responses = result.Unwrap();
                testResults.Add(new(variant, responses));
            }
            bool failed = false;
            foreach (var validation in test.Validations)
            {
                var validator = ValidatorFactory.Create(validation);
                if (await validator.ValidateAsync(testResults, cancellationToken) is Result<Error>.Err { Error: { } error })
                {
                    failed = true;
                    var errorNode = new TestNode
                    {
                        Uid = GetStableUid(test),
                        DisplayName = test.Name,
                        Properties = new PropertyBag(new FailedTestNodeStateProperty(error.Message)),
                    };
                    await context.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(Uid, errorNode)).ConfigureAwait(false);
                }
            }
            if (!failed)
            {
                var successNode = new TestNode
                {
                    Uid = GetStableUid(test),
                    DisplayName = test.Name,
                    Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
                };
                await context.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(Uid, successNode)).ConfigureAwait(false);
            }
        }
    }
}
