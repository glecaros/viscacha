using System.Collections.Generic;
using System.Threading.Tasks;
using Viscacha.Model;
using Viscacha.CLI.Test.Framework;
using Viscacha.CLI.Test.Framework.Validation;
using Viscacha.CLI.Test.Model;

namespace Viscacha.CLI.Test.Tests;

[TestFixture]
public class FieldFormatValidatorTests
{
    [Test]
    public async Task ValidateAsync_ValidJsonField_ReturnsOk()
    {
        Document doc = new (Defaults.Empty, []);
        FieldFormatValidation validation = new("$.foo", Format.Json);
        FieldFormatValidator validator = new(validation);
        var content = new { foo = "{\"bar\":123}" };
        List<TestVariantResult> testResults = [
            new(new("v1", doc), [new(200, content, "application/json", [])]),
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Ok);
    }

    [Test]
    public async Task ValidateAsync_InvalidJsonField_ReturnsError()
    {
        Document doc = new (Defaults.Empty, []);
        FieldFormatValidation validation = new("$.foo", Format.Json);
        FieldFormatValidator validator = new(validation);
        var content = new { foo =  "not-json" };
        List<TestVariantResult> testResults = [
            new(new("v1", doc), [new(200, content, "application/json", [])]),
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Err);
        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("JSON"));
    }
}
