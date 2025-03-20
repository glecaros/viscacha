using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Viscacha.Model;
using Viscacha.Model.Test;

namespace Viscacha.Runner;

public record ValidationError(string Message);

internal record TestVariant(ConfigurationReference Configuration, Document RequestDocument, bool Baseline);
internal record TestDetails(Test Test, List<TestVariant> Variants);

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
        foreach (var test in _testDetails)
        {

        }
        // _suite.Configuration
        // foreach (var test in _suite.Tests)
        // {
        //     RequestExecutor executor = new();
        //     var request = test.Request;
        //     var response = _httpClient.Send(request);
        //     var expected = test.Expected;
        //     if (response.Code != expected.Code)
        //     {
        //         throw new Exception($"Expected status code {expected.Code}, got {response.Code}");
        //     }
        //     if (response.Content != expected.Content)
        //     {
        //         throw new Exception($"Expected content {expected.Content}, got {response.Content}");
        //     }
        // }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}