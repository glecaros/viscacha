using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;
using NUnit.Framework.Internal;
using Viscacha.CLI.Test.Framework;
using Viscacha.CLI.Test.Model;
using Viscacha.Model;

namespace Viscacha.CLI.Test.Tests;

public class SessionTests : TestBase
{
    [Test]
    public async Task InitAsync_FileDoesNotExist_ReturnsError()
    {
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent-suite.yaml");
        var session = new Session(new SessionUid(Guid.NewGuid().ToString()), new(new(nonExistentFile), null));

        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result, Is.InstanceOf<Result<Error>.Err>());

        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("File not found"));
    }

    [Test]
    public async Task InitAsync_ValidSuiteFile_InitializesSuccessfully()
    {
        var suiteContent =
            "variables:\n" +
            "  var1: value1\n" +
            "configurations:\n" +
            "  - name: default\n" +
            "    path: config.yaml\n" +
            "tests:\n" +
            "  - name: test1\n" +
            "    request-file: request.yaml\n" +
            "    configurations: [default]\n" +
            "    validations: []\n";
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c = CreateTestFile("config.yaml", "base-url: https://api.example.com");
        using var _r = CreateTestFile("request.yaml", "method: GET\nurl: /api/test");

        var session = new Session(new SessionUid(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result, Is.InstanceOf<Result<Error>.Ok>());
    }

    [Test]
    public async Task InitAsync_MissingConfigurationFile_ReturnsError()
    {
        var suiteContent = @"
configurations:
  - name: default
    path: missing-config.yaml
tests: []
";
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        var session = new Session(new SessionUid(Guid.NewGuid().ToString()), new(suiteFile, null));

        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result, Is.InstanceOf<Result<Error>.Err>());

        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("File for configuration default not found"));
    }

    [Test]
    public async Task InitAsync_MissingTestRequestFile_ReturnsError()
    {
        var suiteContent = @"
configurations:
  - name: default
    path: config.yaml
tests:
  - name: test1
    request-file: missing-request.yaml
    configurations: [default]
    validations: []
";
        using var _c = CreateTestFile("config.yaml", "base-url: https://api.example.com");
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        var session = new Session(new SessionUid(Guid.NewGuid().ToString()), new(suiteFile, null));

        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result, Is.InstanceOf<Result<Error>.Err>());

        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("File for test test1 not found"));
    }

    [Test]
    public async Task InitAsync_MultipleConfigurations_CreatesOneTestWithMultipleVariants()
    {
        var suiteContent =
            "configurations:\n" +
            "  - name: config1\n" +
            "    path: config1.yaml\n" +
            "  - name: config2\n" +
            "    path: config2.yaml\n" +
            "tests:\n" +
            "  - name: test1\n" +
            "    request-file: request.yaml\n" +
            "    configurations: [config1, config2]\n" +
            "    validations: []\n";
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c1 = CreateTestFile("config1.yaml", "base-url: https://api1.example.com");
        using var _c2 = CreateTestFile("config2.yaml", "base-url: https://api2.example.com");
        using var _r = CreateTestFile("request.yaml", "method: GET\nurl: /api/test");

        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);

        Assert.That(result, Is.InstanceOf<Result<Error>.Ok>());

        var testsField = typeof(Session).GetField("_tests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var tests = testsField?.GetValue(session) as List<FrameworkTest>;

        Assert.That(tests, Is.Not.Null);
        Assert.That(tests!.Count, Is.EqualTo(1), "Should have created exactly one test");
        Assert.That(tests[0].Variants, Has.Count.EqualTo(2), "Test should have exactly two variants");
        Assert.That(tests[0].Variants[0].Name, Is.EqualTo("config1"));
        Assert.That(tests[0].Variants[1].Name, Is.EqualTo("config2"));
    }

    private static List<FrameworkTest> GetTests(Session session)
    {
        var testsField = typeof(Session).GetField("_tests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return testsField?.GetValue(session) as List<FrameworkTest> ?? throw new InvalidOperationException("Failed to retrieve tests");
    }

    [Test]
    public async Task InitAsync_GlobalVariables_AreAppliedToAllTests()
    {
        var suiteContent =
            "variables:\n" +
            "  var1: value1\n" +
            "configurations:\n" +
            "  - name: default\n" +
            "    path: config.yaml\n" +
            "tests:\n" +
            "  - name: test1\n" +
            "    request-file: request1.yaml\n" +
            "    configurations: [default]\n" +
            "    validations: []\n" +
            "  - name: test2\n" +
            "    request-file: request2.yaml\n" +
            "    configurations: [default]\n" +
            "    validations: []\n";

        var configContent =
            "base-url: https://api.example.com\n";
        var request1Content =
            "method: GET\n" +
            "path: /api/${var1}\n";
        var request2Content =
            "method: GET\n" +
            "path: /api/${var1}/test\n";

        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c = CreateTestFile("config.yaml", configContent);
        using var _r1 = CreateTestFile("request1.yaml", request1Content);
        using var _r2 = CreateTestFile("request2.yaml", request2Content);

        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result, Is.InstanceOf<Result<Error>.Ok>());

        var tests = GetTests(session);

        Assert.That(tests, Is.Not.Null);
        Assert.That(tests, Has.Count.EqualTo(2), "Should have created exactly two tests");

        var test1 = tests![0];
        Assert.That(test1.Variants, Has.Count.EqualTo(1), "Test should have exactly one variant");
        var test1Variant1 = test1.Variants[0];
        Assert.That(test1Variant1.Request, Is.Not.Null);
        Assert.That(test1Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant1.Request.Requests[0].Path, Is.EqualTo("/api/value1"));

        var test2 = tests[1];
        Assert.That(test2.Variants, Has.Count.EqualTo(1), "Test should have exactly one variant");
        var test2Variant1 = test2.Variants[0];
        Assert.That(test2Variant1.Request, Is.Not.Null);
        Assert.That(test2Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test2Variant1.Request.Requests[0].Path, Is.EqualTo("/api/value1/test"));
    }

    [Test]
    public async Task InitAsync_TestVariables_AreAppliedToTest()
    {
        var suiteContent =
            "configurations:\n" +
            "  - name: default\n" +
            "    path: config.yaml\n" +
            "tests:\n" +
            "  - name: test1\n" +
            "    variables:\n" +
            "      var1: value1\n" +
            "    request-file: request1.yaml\n" +
            "    configurations: [default]\n" +
            "    validations: []\n" +
            "  - name: test2\n" +
            "    request-file: request2.yaml\n" +
            "    configurations: [default]\n" +
            "    validations: []\n";
        var configContent =
            "base-url: https://api.example.com\n";
        var request1Content =
            "method: GET\n" +
            "path: /api/${var1}\n";
        var request2Content =
            "method: GET\n" +
            "path: /api/${var1}/test\n";

        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c = CreateTestFile("config.yaml", configContent);
        using var _r1 = CreateTestFile("request1.yaml", request1Content);
        using var _r2 = CreateTestFile("request2.yaml", request2Content);

        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result, Is.InstanceOf<Result<Error>.Ok>());

        var tests = GetTests(session);

        Assert.That(tests, Is.Not.Null);
        Assert.That(tests, Has.Count.EqualTo(2), "Should have created exactly two tests");

        var test1 = tests![0];
        Assert.That(test1.Variants, Has.Count.EqualTo(1), "Test should have exactly one variant");
        var test1Variant1 = test1.Variants[0];
        Assert.That(test1Variant1.Request, Is.Not.Null);
        Assert.That(test1Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant1.Request.Requests[0].Path, Is.EqualTo("/api/value1"));

        var test2 = tests[1];
        Assert.That(test2.Variants, Has.Count.EqualTo(1), "Test should have exactly one variant");
        var test2Variant1 = test2.Variants[0];
        Assert.That(test2Variant1.Request, Is.Not.Null);
        Assert.That(test2Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test2Variant1.Request.Requests[0].Path, Is.EqualTo("/api//test")); // Variable not set, should be empty
    }

    [Test]
    public async Task InitAsync_ConfigurationVariables_AreAppliedToVariant()
    {
        var suiteContent =
            "configurations:\n" +
            "  - name: config1\n" +
            "    path: config.yaml\n" +
            "    variables:\n" +
            "      var1: value1\n" +
            "  - name: config2\n" +
            "    path: config.yaml\n" +
            "    variables:\n" +
            "      var1: value2\n" +
            "tests:\n" +
            "  - name: test1\n" +
            "    request-file: request1.yaml\n" +
            "    configurations: [config1, config2]\n" +
            "    validations: []\n";
        var configContent =
            "base-url: https://api.example.com\n";
        var requestContent =
            "method: GET\n" +
            "path: /api/${var1}\n";
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c = CreateTestFile("config.yaml", configContent);
        using var _r = CreateTestFile("request1.yaml", requestContent);

        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));

        await session.InitAsync(CancellationToken.None);
        var tests = GetTests(session);
        Assert.That(tests, Is.Not.Null);
        Assert.That(tests, Has.Count.EqualTo(1), "Should have created exactly one test");

        var test1 = tests![0];
        Assert.That(test1.Variants, Has.Count.EqualTo(2), "Test should have exactly two variants");
        var test1Variant1 = test1.Variants[0];
        Assert.That(test1Variant1.Request, Is.Not.Null);
        Assert.That(test1Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant1.Request.Requests[0].Path, Is.EqualTo("/api/value1"));

        var test1Variant2 = test1.Variants[1];
        Assert.That(test1Variant2.Request, Is.Not.Null);
        Assert.That(test1Variant2.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant2.Request.Requests[0].Path, Is.EqualTo("/api/value2"));
    }

    [Test]
    public async Task InitAsync_TestVariables_OverrideGlobalVariables()
    {
        var suiteContent =
            "variables:\n" +
            "  var1: value1\n" +
            "configurations:\n" +
            "  - name: default\n" +
            "    path: config.yaml\n" +
            "tests:\n" +
            "  - name: test1\n" +
            "    variables:\n" +
            "      var1: value2\n" +
            "    request-file: request1.yaml\n" +
            "    configurations: [default]\n" +
            "    validations: []\n" +
            "  - name: test2\n" +
            "    request-file: request2.yaml\n" +
            "    configurations: [default]\n" +
            "    validations: []\n";
        var configContent =
            "base-url: https://api.example.com\n";
        var request1Content =
            "method: GET\n" +
            "path: /api/${var1}\n";
        var request2Content =
            "method: GET\n" +
            "path: /api/${var1}\n";
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c = CreateTestFile("config.yaml", configContent);
        using var _r1 = CreateTestFile("request1.yaml", request1Content);
        using var _r2 = CreateTestFile("request2.yaml", request2Content);

        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result, Is.InstanceOf<Result<Error>.Ok>());

        var tests = GetTests(session);

        Assert.That(tests, Is.Not.Null);
        Assert.That(tests, Has.Count.EqualTo(2), "Should have created exactly two tests");
        var test1 = tests![0];
        Assert.That(test1.Variants, Has.Count.EqualTo(1), "Test should have exactly one variant");
        var test1Variant1 = test1.Variants[0];
        Assert.That(test1Variant1.Request, Is.Not.Null);
        Assert.That(test1Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant1.Request.Requests[0].Path, Is.EqualTo("/api/value2"));

        var test2 = tests[1];
        Assert.That(test2.Variants, Has.Count.EqualTo(1), "Test should have exactly one variant");
        var test2Variant1 = test2.Variants[0];
        Assert.That(test2Variant1.Request, Is.Not.Null);
        Assert.That(test2Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test2Variant1.Request.Requests[0].Path, Is.EqualTo("/api/value1"));
    }

    [Test]
    public async Task InitAsync_ConfigurationVariables_OverrideGlobalVariables()
    {
        var suiteContent =
            "variables:\n" +
            "  var1: value1\n" +
            "configurations:\n" +
            "  - name: config1\n" +
            "    path: config.yaml\n" +
            "    variables:\n" +
            "      var1: value2\n" +
            "  - name: config2\n" +
            "    path: config.yaml\n" +
            "tests:\n" +
            "  - name: test1\n" +
            "    request-file: request1.yaml\n" +
            "    configurations: [config1, config2]\n" +
            "    validations: []\n";
        var configContent =
            "base-url: https://api.example.com\n";
        var requestContent =
            "method: GET\n" +
            "path: /api/${var1}\n";
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c1 = CreateTestFile("config.yaml", configContent);
        using var _r = CreateTestFile("request1.yaml", requestContent);

        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result, Is.InstanceOf<Result<Error>.Ok>());

        var tests = GetTests(session);

        Assert.That(tests, Is.Not.Null);
        Assert.That(tests, Has.Count.EqualTo(1), "Should have created exactly one test");

        var test1 = tests![0];
        Assert.That(test1.Variants, Has.Count.EqualTo(2), "Test should have exactly two variants");
        var test1Variant1 = test1.Variants[0];
        Assert.That(test1Variant1.Request, Is.Not.Null);
        Assert.That(test1Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant1.Request.Requests[0].Path, Is.EqualTo("/api/value2"));

        var test1Variant2 = test1.Variants[1];
        Assert.That(test1Variant2.Request, Is.Not.Null);
        Assert.That(test1Variant2.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant2.Request.Requests[0].Path, Is.EqualTo("/api/value1"));
    }

    [Test]
    public async Task InitAsync_ConfigurationVariables_OverrideTestVariables()
    {
        var suiteContent =
            "configurations:\n" +
            "  - name: config1\n" +
            "    path: config.yaml\n" +
            "    variables:\n" +
            "      var1: value2\n" +
            "  - name: config2\n" +
            "    path: config.yaml\n" +
            "tests:\n" +
            "  - name: test1\n" +
            "    variables:\n" +
            "      var1: value1\n" +
            "    request-file: request1.yaml\n" +
            "    configurations: [config1, config2]\n" +
            "    validations: []\n";
        var configContent =
            "base-url: https://api.example.com\n";
        var requestContent =
            "method: GET\n" +
            "path: /api/${var1}\n";
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c1 = CreateTestFile("config.yaml", configContent);
        using var _r = CreateTestFile("request1.yaml", requestContent);

        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);

        Assert.That(result, Is.InstanceOf<Result<Error>.Ok>());
        var tests = GetTests(session);
        Assert.That(tests, Is.Not.Null);
        Assert.That(tests, Has.Count.EqualTo(1), "Should have created exactly one test");

        var test1 = tests![0];

        Assert.That(test1.Variants, Has.Count.EqualTo(2), "Test should have exactly two variants");
        var test1Variant1 = test1.Variants[0];
        Assert.That(test1Variant1.Request, Is.Not.Null);
        Assert.That(test1Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant1.Request.Requests[0].Path, Is.EqualTo("/api/value2"));

        var test1Variant2 = test1.Variants[1];
        Assert.That(test1Variant2.Request, Is.Not.Null);
        Assert.That(test1Variant2.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant2.Request.Requests[0].Path, Is.EqualTo("/api/value1"));
    }

    [Test]
    public async Task InitAsync_ValidationWithOptionalParameter()
    {
        var suiteContent =
            "variables:\n" +
            "  var1: value1\n" +
            "configurations:\n" +
            "  - name: config1\n" +
            "    path: config.yaml\n" +
            "  - name: config2\n" +
            "    path: config.yaml\n" +
            "tests:\n" +
            "  - name: test1\n" +
            "    request-file: request.yaml\n" +
            "    configurations: [config1, config2]\n" +
            "    validations:\n" +
            "      - type: path-comparison\n" +
            "        baseline: /api/value1\n" +
            "        ignore-paths:\n" +
            "          - foo\n" +
            "        preserve-array-indices: true\n";
        var configContent =
            "base-url: https://api.example.com\n";
        var requestContent =
            "method: GET\n" +
            "path: /api/value1\n";
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c1 = CreateTestFile("config.yaml", configContent);
        using var _r = CreateTestFile("request.yaml", requestContent);
        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result, Is.InstanceOf<Result<Error>.Ok>());
        var tests = GetTests(session);
        Assert.That(tests, Is.Not.Null);
        Assert.That(tests, Has.Count.EqualTo(1), "Should have created exactly one test");
        var test1 = tests![0];
        Assert.That(test1.Variants, Has.Count.EqualTo(2), "Test should have exactly two variants");
        var test1Variant1 = test1.Variants[0];
        Assert.That(test1Variant1.Request, Is.Not.Null);
        Assert.That(test1Variant1.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant1.Request.Requests[0].Path, Is.EqualTo("/api/value1"));
        var test1Variant2 = test1.Variants[1];
        Assert.That(test1Variant2.Request, Is.Not.Null);
        Assert.That(test1Variant2.Request.Requests, Has.Count.EqualTo(1), "Request should have exactly one request");
        Assert.That(test1Variant2.Request.Requests[0].Path, Is.EqualTo("/api/value1"));

        Assert.That(test1.Validations, Has.Count.EqualTo(1), "Test should have exactly one validation");
        var validation = test1.Validations[0];
        Assert.That(validation, Is.InstanceOf<PathComparisonValidation>(), "Validation should be of type PathComparisonValidation");
        var pathComparisonValidation = (PathComparisonValidation)validation;
        Assert.That(pathComparisonValidation.Baseline, Is.EqualTo("/api/value1"), "Baseline should be /api/value1");
        Assert.That(pathComparisonValidation.IgnorePaths, Has.Count.EqualTo(1), "Ignore paths should have one entry");
        Assert.That(pathComparisonValidation.IgnorePaths, Has.Member("foo"), "Ignore paths should contain 'foo'");
        Assert.That(pathComparisonValidation.PreserveArrayIndices, Is.True, "Preserve array indices should be true");
    }

    internal class MockDataProducer : IDataProducer
    {
        public Type[] DataTypesProduced => Array.Empty<Type>();

        public string Uid => "mock-producer";

        public string Version => "1.0";

        public string DisplayName => "Mock Data Producer";

        public string Description => "Mock implementation for testing.";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    internal class MockRequest : IRequest
    {
        public TestSessionContext Session { get; } = null!;
    }

    internal class MockMessageBus : IMessageBus
    {
        public List<IData> PublishedData { get; } = new();

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            PublishedData.Add(data);
            return Task.CompletedTask;
        }
    }

    internal class MockRequestCompletionNotifier : IExecuteRequestCompletionNotifier
    {
        public bool IsCompleted { get; private set; } = false;

        public void Complete()
        {
            IsCompleted = true;
        }
    }

    [Test]
    public async Task DiscoverTestsAsync_ValidRequestFile_ReturnsTestList()
    {
        var suiteContent = @"
configurations:
  - name: default
    path: config.yaml
tests:
  - name: test1
    request-file: request.yaml
    configurations: [default]
    validations: []
";

        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c = CreateTestFile("config.yaml", "base-url: https://api.example.com");
        using var _r = CreateTestFile("request.yaml", "method: GET\nurl: /api/test");

        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));
        var initResult = await session.InitAsync(CancellationToken.None);
        Assert.That(initResult, Is.InstanceOf<Result<Error>.Ok>());

        MockDataProducer producer = new();
        MockRequest request = new();
        MockMessageBus messageBus = new();
        MockRequestCompletionNotifier notifier = new();
        ExecuteRequestContext context = new(request, messageBus, notifier, CancellationToken.None);

        await session.DiscoverTestsAsync(producer, context, CancellationToken.None);

        Assert.That(messageBus.PublishedData.Count, Is.EqualTo(1));
        var publishedMessage = messageBus.PublishedData[0] as TestNodeUpdateMessage;
        Assert.That(publishedMessage, Is.Not.Null);
        Assert.That(publishedMessage!.TestNode.DisplayName, Is.EqualTo("test1"));
        Assert.That(publishedMessage.TestNode.Uid.Value, Does.Contain("test1"));
    }
}
