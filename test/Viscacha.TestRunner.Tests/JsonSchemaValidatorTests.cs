using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Viscacha.Model;
using Viscacha.TestRunner.Framework;
using Viscacha.TestRunner.Framework.Validation;
using Viscacha.TestRunner.Model;

namespace Viscacha.TestRunner.Tests;

[TestFixture]
public class JsonSchemaValidatorTests : TestBase
{
    [Test]
    public async Task ValidateAsync_SelfContained_PayloadMatches_ReturnsOk()
    {
        FileInfo schemaFile = new("data/self-contained.json");
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException("Schema file not found", schemaFile.FullName);
        }

        Document doc = new(Defaults.Empty, []);
        JsonSchemaValidation validation = new(new SelfContainedJsonSchema(schemaFile.FullName));
        JsonSchemaValidator validator = new(validation);
        var content = new
        {
            firstName = "Bilbo",
            lastName = "Baggins",
            age = 111,
            nickNames = new[] { "Ringbearer", "Cousin Bilbo" },
            address = new
            {
                street = "Bag End",
                city = "Hobbiton",
                country = "Shire",
            },
        };
        List<TestVariantResult> testResults = [
            new(new("v1", doc), [new(200, content, "application/json", [])])
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Ok);
    }

    [Test]
    public async Task ValidateAsync_SelfContained_PayloadMismatch_ReturnsError()
    {
        FileInfo schemaFile = new("data/self-contained.json");
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException("Schema file not found", schemaFile.FullName);
        }

        Document doc = new(Defaults.Empty, []);
        JsonSchemaValidation validation = new(new SelfContainedJsonSchema(schemaFile.FullName));
        JsonSchemaValidator validator = new(validation);
        var content = new
        {
            firstName = "Bilbo",
            age = 111,
            nickNames = new[] { "Ringbearer", "Cousin Bilbo" },
            address = new
            {
                street = "Bag End",
                city = "Hobbiton",
                country = "Shire",
            },
        };
        List<TestVariantResult> testResults = [
            new(new("v1", doc), [new(200, content, "application/json", [])])
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Err);
        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("Validation failed for response"));
    }

    [Test]
    public async Task ValidateAsync_Bundle_PayloadMatches_ReturnsOk()
    {
        FileInfo schemaFile = new("data/bundle.json");
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException("Schema file not found", schemaFile.FullName);
        }

        Document doc = new(Defaults.Empty, []);
        JsonSchemaValidation validation = new(new BundleJsonSchema(schemaFile.FullName, "#/$defs/Person"));
        JsonSchemaValidator validator = new(validation);
        var content = new
        {
            firstName = "Bilbo",
            lastName = "Baggins",
            age = 111,
            nickNames = new[] { "Ringbearer", "Cousin Bilbo" },
            address = new
            {
                street = "Bag End",
                city = "Hobbiton",
                country = "Shire",
            },
        };
        List<TestVariantResult> testResults = [
            new(new("v1", doc), [new(200, content, "application/json", [])])
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Ok);
    }

    [Test]
    public async Task ValidateAsync_Bundle_PayloadMismatch_ReturnsError()
    {
        FileInfo schemaFile = new("data/bundle.json");
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException("Schema file not found", schemaFile.FullName);
        }

        Document doc = new(Defaults.Empty, []);
        JsonSchemaValidation validation = new(new BundleJsonSchema(schemaFile.FullName, "#/$defs/Person"));
        JsonSchemaValidator validator = new(validation);
        var content = new
        {
            firstName = "Bilbo",
            age = 111,
            nickNames = new[] { "Ringbearer", "Cousin Bilbo" },
            address = new
            {
                street = "Bag End",
                city = "Hobbiton",
                country = "Shire",
            },
        };
        List<TestVariantResult> testResults = [
            new(new("v1", doc), [new(200, content, "application/json", [])])
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Err);
        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("Validation failed for response"));
    }

    [Test]
    public async Task ValidateAsync_MultiFile_PayloadMatches_ReturnsOk()
    {
        FileInfo schemaFile = new("data/single-definitions/Person.json");
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException("Schema file not found", schemaFile.FullName);
        }
        List<FileInfo> dependencies =
        [
            new("data/single-definitions/Address.json"),
            new("data/single-definitions/Car.json"),
        ];
        foreach (var dependency in dependencies)
        {
            if (!dependency.Exists)
            {
                throw new FileNotFoundException("Dependency file not found", dependency.FullName);
            }
        }

        Document doc = new(Defaults.Empty, []);
        JsonSchemaValidation validation = new(new MultiFileJsonSchema(schemaFile.FullName, [.. dependencies.Select(f => f.FullName)]));
        JsonSchemaValidator validator = new(validation);
        var content = new
        {
            firstName = "Bilbo",
            lastName = "Baggins",
            age = 111,
            nickNames = new[] { "Ringbearer", "Cousin Bilbo" },
            address = new
            {
                street = "Bag End",
                city = "Hobbiton",
                country = "Shire",
            },
        };
        List<TestVariantResult> testResults = [
            new(new("v1", doc), [new(200, content, "application/json", [])])
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Ok);
    }

    [Test]
    public async Task ValidateAsync_MultiFile_PayloadMismatch_ReturnsError()
    {
        FileInfo schemaFile = new("data/single-definitions/Person.json");
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException("Schema file not found", schemaFile.FullName);
        }
        List<FileInfo> dependencies =
        [
            new("data/single-definitions/Address.json"),
            new("data/single-definitions/Car.json"),
        ];
        foreach (var dependency in dependencies)
        {
            if (!dependency.Exists)
            {
                throw new FileNotFoundException("Dependency file not found", dependency.FullName);
            }
        }

        Document doc = new(Defaults.Empty, []);
        JsonSchemaValidation validation = new(new MultiFileJsonSchema(schemaFile.FullName, [.. dependencies.Select(f => f.FullName)]));
        JsonSchemaValidator validator = new(validation);
        var content = new
        {
            firstName = "Bilbo",
            age = 111,
            nickNames = new[] { "Ringbearer", "Cousin Bilbo" },
            address = new
            {
                street = "Bag End",
                city = "Hobbiton",
                country = "Shire",
            },
        };
        List<TestVariantResult> testResults = [
            new(new("v1", doc), [new(200, content, "application/json", [])])
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Err);
        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("Validation failed for response"));
    }
}
