using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Viscacha.Model;
using Viscacha.TestRunner.Framework;
using Viscacha.TestRunner.Framework.Validation;
using Viscacha.TestRunner.Model;

namespace Viscacha.TestRunner.Tests;

[TestFixture]
public class JsonSchemaValidatorTests: TestBase
{
    [Test]
    public async Task ValidateAsync_PayloadMatches_ReturnsOk()
    {
        FileInfo schemaFile = new("data/self-contained.json");
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException("Schema file not found", schemaFile.FullName);
        }

        Document doc = new (Defaults.Empty, []);
        JsonSchemaValidation validation = new(schemaFile.FullName);
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
    public async Task ValidateAsync_PayloadMismatch_ReturnsError()
    {
        FileInfo schemaFile = new("data/self-contained.json");
        if (!schemaFile.Exists)
        {
            throw new FileNotFoundException("Schema file not found", schemaFile.FullName);
        }

        Document doc = new (Defaults.Empty, []);
        JsonSchemaValidation validation = new(schemaFile.FullName);
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
