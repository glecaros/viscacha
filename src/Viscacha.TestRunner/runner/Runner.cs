using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Azure;
using Viscacha.Model;
using Viscacha.Model.Test;

namespace Viscacha.Runner;

public record ValidationError(string Message);

internal record TestVariant(ConfigurationReference Configuration, Document RequestDocument, bool Baseline);
internal record TestDetails(Test Test, List<TestVariant> Variants);

internal record TestVariantResult(ConfigurationReference Configuration, List<ResponseWrapper> Responses, bool Baseline);
internal record TestResult(Test Test, List<TestVariantResult> Variants);

public class Runner(Suite suite, HttpClient? httpClient = null) : IDisposable
{
    private readonly Suite _suite = suite;
    private readonly HttpClient _httpClient = httpClient ?? new();

    private List<TestDetails> _testDetails = [];

    public Result<ValidationError> Validate()
    {
        var configurations = _suite.Configurations.ToDictionary(c => c.Name);
        List<TestDetails> testDetails = [];
        foreach (var test in _suite.Tests)
        {
            var variables = _suite.Variables.Merge(test.Variables) ?? [];
            var parser = new DocumentParser(variables);
            List<TestVariant> testVariants = [];
            foreach (var conf in test.Configurations)
            {
                if (!configurations.TryGetValue(conf, out var configuration))
                {
                    return new ValidationError($"Configuration {conf} required by test {test.Name} not found");
                }
                var requestDocument = parser.FromFile(new(test.RequestFile), new(configuration.Path));
                if (requestDocument is Result<Document, Error>.Err error)
                {
                    return new ValidationError(error.Error.Message);
                }
                testVariants.Add(new TestVariant(configuration, requestDocument.Unwrap(), configuration.Baseline ?? false));
            }
            testDetails.Add(new TestDetails(test, testVariants));
        }
        testDetails = _testDetails;
        return new Result<ValidationError>.Ok();
    }

    public void Run()
    {
        using HttpClient client = new();
        foreach (var test in _testDetails)
        {
            List<TestVariantResult> testVariantResults = [];
            foreach (var (configuration, doc, baseline) in test.Variants)
            {
                var executor = new RequestExecutor(doc.Defaults);
                var responses = new List<ResponseWrapper>();
                foreach (var request in doc.Requests)
                {
                    var result = executor.Execute(client, request, doc.Requests.IndexOf(request));
                    if (result is Result<ResponseWrapper, Error>.Err error)
                    {
                        throw new Exception($"Error executing request: {error.Error.Message}");
                    }
                    var response = result.Unwrap();
                    if (response is null)
                    {
                        throw new Exception("Response is null");
                    }
                    responses.Add(response);
                }
                testVariantResults.Add(new(configuration, responses, baseline));
            }
            foreach (var validation in test.Test.Validations)
            {
                switch (validation)
                {
                    case StatusValidation statusValidation:
                    {
                        break;
                    }
                    case PathValidation pathValidation:
                    {
                        break;
                    }
                    case FormatValidation formatValidation:
                    {
                        break;
                    }
                    case SemanticValidation semanticValidation:
                    {
                        break;
                    }
                }
            }

        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}