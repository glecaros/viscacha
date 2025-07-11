using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.TestHost;
using Viscacha.Model;
using Viscacha.CLI.Test.Framework.Validation;
using Viscacha.CLI.Test.Model;
using Viscacha.CLI.Test.Util;
using YAYL;

namespace Viscacha.CLI.Test.Framework;

internal record FrameworkTestVariant(string Name, Document Request);
internal record FrameworkTest(string Name, List<FrameworkTestVariant> Variants, List<ValidationDefinition> Validations, bool Skip);
internal record TestVariantResult(FrameworkTestVariant Variant, List<ResponseWrapper> Responses);

internal sealed class Session(SessionUid uid, SessionOptions options)
{
    public SessionUid Uid { get; } = uid;

    private bool _failed = false;
    private bool _initialized = false;
    private List<FrameworkTest> _tests = [];

    private readonly FileInfo _inputFile = options.InputFile;
    private readonly DirectoryInfo? _responsesDirectory = options.ResponsesDirectory;

    private async Task<Result<Error>> InitAsyncInternal(CancellationToken cancellationToken)
    {
        var suiteFileDirectory = _inputFile.DirectoryName ?? string.Empty;
        var parser = new Parser(null, new(suiteFileDirectory));
        try {
            var suiteResult = await parser.TryParseFileAsync<Suite>(_inputFile.FullName, cancellationToken).ConfigureAwait(false);
            if (suiteResult is Result<Suite?, Error>.Err { Error: { } _ })
            {
                return suiteResult.UnwrapError();
            }
            var suite = suiteResult.Unwrap();

            Dictionary<string, (FileInfo File, Dictionary<string, string>? Variables)> configurations = [];
            foreach (var configuration in suite.Configurations)
            {
                var filePath = Path.Combine(suiteFileDirectory, configuration.Path);
                if (!File.Exists(filePath))
                {
                    return new Error($"File for configuration {configuration.Name} not found: {filePath}");
                }
                configurations[configuration.Name] = (new FileInfo(filePath), configuration.Variables);
            }
            List<FrameworkTest> tests = [];
            foreach (var test in suite.Tests)
            {
                var testVariables = suite.Variables.Merge(test.Variables) ?? [];
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
                    var variantVariables = testVariables.Merge(configuration.Variables) ?? [];
                    var documentParser = new DocumentParser(variantVariables);
                    switch (await documentParser.FromFileAsync(testFile, configuration.File, cancellationToken).ConfigureAwait(false))
                    {
                        case Result<Document, Error>.Err error:
                            return error.Error;
                        case Result<Document, Error>.Ok document:
                            testVariants.Add(new(variant, document.Value));

                            break;
                    }
                }
                tests.Add(new(test.Name, testVariants, test.Validations, test.Skip ?? false));
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

    public async Task<Result<Error>> InitAsync(CancellationToken cancellationToken){
        if (_initialized)
        {
            return new Error("Session already initialized.");
        }
        if (_failed)
        {
            return new Error("Session failed to initialize.");
        }
        return (await InitAsyncInternal(cancellationToken).ConfigureAwait(false)).Else<Error>(e =>
        {
            _failed = true;
            return e;
        });
    }

    private string GetStableUid(FrameworkTest test)
    {
        return $"{_inputFile.FullName}.{test.Name}";
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

    private async Task<Result<Error>> SaveResponsesAsync(IDataProducer producer, ExecuteRequestContext context, FrameworkTest test, FrameworkTestVariant variant, List<ResponseWrapper> responses, CancellationToken cancellationToken)
    {
        if (_responsesDirectory is null)
        {
            return new Result<Error>.Ok();
        }
        var variantDirectory = Path.Combine(_responsesDirectory.FullName, test.Name, variant.Name);
        try
        {
            Directory.CreateDirectory(variantDirectory);
        }
        catch (Exception e)
        {
            return new Error($"Failed to create directory for responses: {e.Message}");
        }
        var options = new JsonSerializerOptions() { WriteIndented = true };
        foreach (var (index, response) in responses.Enumerate())
        {
            var fileName = $"response_{index}.json";
            var filePath = Path.Combine(variantDirectory, fileName);
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(response, options), cancellationToken).ConfigureAwait(false);
            var node = new TestNode
            {
                Uid = GetStableUid(test),
                DisplayName = test.Name,
                Properties = new(new FileArtifactProperty(new(filePath), $"Response index {index} for variant {variant.Name}")),
            };
            await context.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(Uid, node)).ConfigureAwait(false);
        }
        return new Result<Error>.Ok();
    }

    private static async Task<Result<List<ResponseWrapper>, Error>> ExecuteVariantAsync(HttpClient client, FrameworkTestVariant variant, CancellationToken cancellationToken)
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
            if (test.Skip)
            {
                var skippedNode = new TestNode
                {
                    Uid = GetStableUid(test),
                    DisplayName = test.Name,
                    Properties = new PropertyBag(SkippedTestNodeStateProperty.CachedInstance),
                };
                await context.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(Uid, skippedNode)).ConfigureAwait(false);
                continue;
            }
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
                if (result is Result<List<ResponseWrapper>, Error>.Err { Error: { } })
                {
                    var errorNode = new TestNode
                    {
                        Uid = GetStableUid(test),
                        DisplayName = test.Name,
                        Properties = new(new FailedTestNodeStateProperty(result.UnwrapError().Message)),
                    };
                    await context.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(Uid, errorNode)).ConfigureAwait(false);
                    continue;
                }
                var responses = result.Unwrap();
                if (await SaveResponsesAsync(producer, context, test, variant, responses, cancellationToken) is Result<Error>.Err { Error: { } error })
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
                testResults.Add(new(variant, responses));
            }

            foreach (var validation in test.Validations)
            {
                var validator = ValidatorFactory.Create(validation);
                if (await validator.ValidateAsync(testResults, cancellationToken) is Result<Error>.Err { Error: { } error })
                {
                    var errorNode = new TestNode
                    {
                        Uid = GetStableUid(test),
                        DisplayName = test.Name,
                        Properties = new PropertyBag(new FailedTestNodeStateProperty(error.Message)),
                    };
                    await context.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(Uid, errorNode)).ConfigureAwait(false);
                    return;
                }
            }
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
