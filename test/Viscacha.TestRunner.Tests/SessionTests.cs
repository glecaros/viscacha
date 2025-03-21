using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.TestRunner.Framework;
using Viscacha.Model;
using NUnit.Framework;
using Microsoft.Testing.Platform.TestHost;

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
        var session = new Session(new SessionUid(Guid.NewGuid().ToString()));
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent-suite.yaml");

        var result = await session.InitAsync(nonExistentFile, CancellationToken.None);
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
        var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        CreateTestFile("config.yaml", "base-url: https://api.example.com");
        CreateTestFile("request.yaml", "method: GET\nurl: /api/test");

        var session = new Session(new SessionUid(Guid.NewGuid().ToString()));
        var result = await session.InitAsync(suiteFile.FullName, CancellationToken.None);
        Assert.That(result is Result<Error>.Ok);
    }

    [Test]
    public async Task InitAsync_MissingConfigurationFile_ReturnsError()
    {
        var session = new Session(new SessionUid(Guid.NewGuid().ToString()));
        var suiteContent = @"
configurations:
  - name: default
    path: missing-config.yaml
tests: []
";
        var suiteFile = CreateTestFile("suite.yaml", suiteContent);

        var result = await session.InitAsync(suiteFile.FullName, CancellationToken.None);
        Assert.That(result is Result<Error>.Err);

        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("File for configuration default not found"));
    }

    [Test]
    public async Task InitAsync_MissingTestRequestFile_ReturnsError()
    {
        var session = new Session(new SessionUid(Guid.NewGuid().ToString()));
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
        var suiteFile = CreateTestFile("suite.yaml", suiteContent);
        CreateTestFile("config.yaml", "base-url: https://api.example.com");

        var result = await session.InitAsync(suiteFile.FullName, CancellationToken.None);
        Assert.That(result is Result<Error>.Err);

        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("File for test test1 not found"));
    }

    private FileInfo CreateTestFile(string filename, string content)
    {
        var filePath = Path.Combine(_tempDirectory, filename);
        File.WriteAllText(filePath, content);
        return new FileInfo(filePath);
    }
}