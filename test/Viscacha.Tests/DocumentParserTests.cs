using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Viscacha.Model;

namespace Viscacha.Tests;

public class DocumentParserTests
{
    private string _tempDirectory;
    private Dictionary<string, string> _variables;

    [SetUp]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);

        _variables = new Dictionary<string, string>
        {
            { "test_var", "test_value" },
            { "api_key", "secret_key_123" }
        };

        Environment.SetEnvironmentVariable("TEST_ENV_VAR", "env_value");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }

        Environment.SetEnvironmentVariable("TEST_ENV_VAR", null);
    }

    [Test]
    public void FromFile_FileDoesNotExist_ReturnsError()
    {
        var parser = new DocumentParser(_variables);
        var nonExistentFile = new FileInfo(Path.Combine(_tempDirectory, "nonexistent.yaml"));

        var result = parser.FromFile(nonExistentFile, null);
        Assert.That(result is Result<Document, Error>.Err);

        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("File not found"));
    }

    [Test]
    public void FromFile_DefaultsFileDoesNotExist_ReturnsError()
    {
        var parser = new DocumentParser(_variables);
        var yamlContent = "requests:\n  - method: GET\n    url: /api/test";
        var testFile = CreateTestFile("test.yaml", yamlContent);
        var nonExistentDefaultsFile = new FileInfo(Path.Combine(_tempDirectory, "nonexistent-defaults.yaml"));

        var result = parser.FromFile(testFile, nonExistentDefaultsFile);
        Assert.That(result is Result<Document, Error>.Err);

        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("Defaults file not found"));
    }

    [Test]
    public void FromFile_ValidDocumentFile_ParsesSuccessfully()
    {
        var parser = new DocumentParser(_variables);
        var yamlContent =
            "defaults:\n" +
            "  base-url: https://api.example.com\n" +
            "  headers:\n" +
            "    Accept: application/json\n" +
            "requests:\n" +
            "  - method: GET\n" +
            "    path: /users\n" +
            "  - method: POST\n" +
            "    path: /users\n";
        var testFile = CreateTestFile("document.yaml", yamlContent);

        var result = parser.FromFile(testFile, null);
        Assert.That(result is Result<Document, Error>.Ok);

        var document = result.Unwrap();
        Assert.That(document.Requests.Count, Is.EqualTo(2));
        Assert.That(document.Defaults?.BaseUrl, Is.EqualTo("https://api.example.com"));
    }

    [Test]
    public void FromFile_RequestOnlyFile_ParsesAsDocumentWithSingleRequest()
    {
        var parser = new DocumentParser(_variables);
        var yamlContent =
            "method: GET\n" +
            "url: /api/test\n" +
            "headers:\n" +
            "  x-some-header: value\n";
        var testFile = CreateTestFile("request.yaml", yamlContent);

        var result = parser.FromFile(testFile, null);

        Assert.That(result is Result<Document, Error>.Ok);
        var document = result.Unwrap();
        Assert.That(document.Requests.Count, Is.EqualTo(1));
        var request = document.Requests.First()!;
        Assert.That(request.Method, Is.EqualTo("GET"));
        Assert.That(request.Url, Is.EqualTo("/api/test"));
    }

    [Test]
    public void FromFile_WithDefaultsFile_MergesDefaults()
    {
        var parser = new DocumentParser(_variables);

        var documentContent =
            "defaults:\n" +
            "  headers:\n" +
            "    X-Custom: custom-value\n" +
            "requests:\n" +
            "  - method: GET\n" +
            "    url: /api/test\n";
        var testFile = CreateTestFile("with-defaults.yaml", documentContent);

        var defaultsContent =
            "base-url: https://api.example.com\n" +
            "headers:\n" +
            "  Accept: application/json\n" +
            "content-type: application/json\n";
        var defaultsFile = CreateTestFile("defaults.yaml", defaultsContent);

        var result = parser.FromFile(testFile, defaultsFile);
        Assert.That(result is Result<Document, Error>.Ok);

        var document = result.Unwrap();
        Assert.That(document.Defaults, Is.Not.Null);
        var defaults = document.Defaults!;
        Assert.That(defaults.BaseUrl, Is.EqualTo("https://api.example.com"));
        Assert.That(defaults.ContentType, Is.EqualTo("application/json"));
        Assert.That(defaults.Headers?["Accept"], Is.EqualTo("application/json"));
        Assert.That(defaults.Headers?["X-Custom"], Is.EqualTo("custom-value"));
    }

    [Test]
    public void FromFile_WithImportedDefaults_ImportsAndMergesDefaults()
    {
        var parser = new DocumentParser(_variables);

        var importedContent =
            "base-url: https://imported-api.example.com\n" +
            "headers:\n" +
            "  X-Imported: imported-value\n";
        var importedFile = CreateTestFile("imported.yaml", importedContent);

        var documentContent =
            "defaults:\n" +
            "  import: " + Path.GetFileName(importedFile.FullName) + "\n" +
            "  headers:\n" +
            "    X-Custom: custom-value\n" +
            "requests:\n" +
            "  - method: GET\n" +
            "    url: /api/test\n";
        var testFile = CreateTestFile("with-import.yaml", documentContent);

        var result = parser.FromFile(testFile, null);
        Assert.That(result is Result<Document, Error>.Ok);

        var document = result.Unwrap();
        Assert.That(document.Defaults, Is.Not.Null);

        var defaults = document.Defaults!;
        Assert.That(defaults.BaseUrl, Is.EqualTo("https://imported-api.example.com"));
        Assert.That(defaults.Headers?["X-Imported"], Is.EqualTo("imported-value"));
        Assert.That(defaults.Headers?["X-Custom"], Is.EqualTo("custom-value"));
    }

    [Test]
    public void FromFile_WithVariableResolution_ResolvesVariables()
    {
        var parser = new DocumentParser(_variables);

        var yamlContent =
            "defaults:\n" +
            "  base-url: https://api.example.com\n" +
            "  authentication:\n" +
            "    type: api-key\n" +
            "    key: ${api_key}\n" +
            "  headers:\n" +
            "    X-Environment: ${env:TEST_ENV_VAR}\n" +
            "requests:\n" +
            "  - method: GET\n" +
            "    path: /users/${test_var}\n";
        var testFile = CreateTestFile("variables.yaml", yamlContent);

        var result = parser.FromFile(testFile, null);
        Assert.That(result is Result<Document, Error>.Ok);

        var document = result.Unwrap();
        Assert.That(document.Defaults, Is.Not.Null);

        var defaults = document.Defaults!;
        Assert.That(defaults.Headers, Is.Not.Null);
        Assert.That(defaults.Headers!["X-Environment"], Is.EqualTo("env_value"));
        Assert.That(defaults.Authentication is ApiKeyAuthentication);

        var apiKeyAuth = (ApiKeyAuthentication)defaults.Authentication!;
        Assert.That(apiKeyAuth.Key, Is.EqualTo("secret_key_123"));

        Assert.That(document.Requests.Count, Is.EqualTo(1));

        var request = document.Requests.First()!;
        Assert.That(request.Path, Is.EqualTo("/users/test_value"));
    }

    private FileInfo CreateTestFile(string filename, string content)
    {
        var filePath = Path.Combine(_tempDirectory, filename);
        File.WriteAllText(filePath, content);
        return new FileInfo(filePath);
    }
}
