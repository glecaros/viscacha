using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.TestHost;
using Viscacha.Model;
using Viscacha.Model.Test;
using YAYL;

namespace Viscacha.TestRunner.Framework;

internal record FrameworkTestVariant(string Name, Document Request, bool Baseline);
internal record FrameworkTest(string Name, List<FrameworkTestVariant> Variants, List<Validation> Validations);

internal sealed class Session(SessionUid uid)
{
    public SessionUid Uid { get; } = uid;

    private bool _failed = false;
    private bool _initialized = false;
    private List<FrameworkTest> _tests = [];

    private async Task<Result<Error>> InitAsyncInternal(string path, CancellationToken cancellationToken)
    {
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
                if (!File.Exists(configuration.Path))
                {
                    return new Error($"File for configuration {configuration.Name} not found: {configuration.Path}");
                }
                configurations[configuration.Name] = (new FileInfo(configuration.Path), configuration.Baseline ?? false);
            }
            List<FrameworkTest> tests = [];
            foreach (var test in suite.Tests)
            {
                var variables = suite.Variables.Merge(test.Variables) ?? [];
                var documentParser = new DocumentParser(variables);
                FileInfo? testFile;
                if (File.Exists(test.RequestFile))
                {
                    testFile = new FileInfo(test.RequestFile);
                }
                else
                {
                    return new Error($"File for test {test.Name} not found: {test.RequestFile}");
                }

                foreach (var variant in test.Configurations)
                {
                    if (!configurations.TryGetValue(variant, out var configuration))
                    {
                        return new Error($"Configuration {variant} required by test {test.Name} not found");
                    }
                    switch (await documentParser.FromFileAsync(testFile, configuration.File,  cancellationToken))
                    {
                        case Result<Document, Error>.Err error:
                            return error.Error;
                        case Result<Document, Error>.Ok document:
                            tests.Add(new FrameworkTest(test.Name, [new FrameworkTestVariant(variant, document.Value, configuration.Baseline)], test.Validations));
                            break;
                    }
                }
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

}