using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.TestRunner.Framework;
using Viscacha.Model;
using Microsoft.Testing.Platform.TestHost;
using System.Collections.Generic;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using NUnit.Framework.Internal;

namespace Viscacha.TestRunner.Tests;

public class SessionTests
{
    private string _tempDirectory;

    [SetUp]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Test]
    public async Task InitAsync_FileDoesNotExist_ReturnsError()
    {
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent-suite.yaml");
        var session = new Session(new SessionUid(Guid.NewGuid().ToString()), new(new(nonExistentFile), null));

        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result is Result<Error>.Err);

        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("File not found"));
    }

    [Test]
    public async Task InitAsync_ValidSuiteFile_InitializesSuccessfully()
    {
        var suiteContent = @"
variables:
  var1: value1
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

        var session = new Session(new SessionUid(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);
        Assert.That(result is Result<Error>.Ok);
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
        Assert.That(result is Result<Error>.Err);

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
        Assert.That(result is Result<Error>.Err);

        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("File for test test1 not found"));
    }

    [Test]
    public async Task InitAsync_MultipleConfigurations_CreatesOneTestWithMultipleVariants()
    {
        var suiteContent = @"
configurations:
  - name: config1
    path: config1.yaml
  - name: config2
    path: config2.yaml
tests:
  - name: test1
    request-file: request.yaml
    configurations: [config1, config2]
    validations: []
";
        using var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        using var _c1 = CreateTestFile("config1.yaml", "base-url: https://api1.example.com");
        using var _c2 = CreateTestFile("config2.yaml", "base-url: https://api2.example.com");
        using var _r = CreateTestFile("request.yaml", "method: GET\nurl: /api/test");

        Session session = new(new(Guid.NewGuid().ToString()), new(suiteFile, null));
        var result = await session.InitAsync(CancellationToken.None);

        Assert.That(result is Result<Error>.Ok);

        var testsField = typeof(Session).GetField("_tests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var tests = testsField?.GetValue(session) as List<FrameworkTest>;

        Assert.That(tests, Is.Not.Null);
        Assert.That(tests!.Count, Is.EqualTo(1), "Should have created exactly one test");
        Assert.That(tests[0].Variants.Count, Is.EqualTo(2), "Test should have exactly two variants");
        Assert.That(tests[0].Variants[0].Name, Is.EqualTo("config1"));
        Assert.That(tests[0].Variants[1].Name, Is.EqualTo("config2"));
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
        Assert.That(initResult is Result<Error>.Ok);

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

    internal class TestFile(string path, string content): IDisposable
    {
        public string Path { get; } = path;
        public string Content { get; } = content;

        public FileInfo ToFileInfo() => new(Path);

        public static implicit operator FileInfo(TestFile testFile) => testFile.ToFileInfo();
        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }

    private TestFile CreateTestFile(string filename, string content)
    {
        var filePath = Path.Combine(_tempDirectory, filename);
        return new TestFile(filePath, content);
    }
}