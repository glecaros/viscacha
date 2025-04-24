using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Viscacha.Model.Test;
using Viscacha.TestRunner.Framework;
using Viscacha.TestRunner.Framework.Validation;

namespace Viscacha.TestRunner.Tests;

[TestFixture]
public class FieldFormatValidatorTests
{
    // [Test]
    // public async Task ValidateAsync_ValidJsonField_ReturnsOk()
    // {
    //     var validation = new FieldFormatValidation("foo", Format.Json);
    //     var validator = new FieldFormatValidator(validation);
    //     var content = JsonSerializer.Deserialize<JsonElement>("{\"foo\": \"{\\\"bar\\\":123}\"}");
    //     var testResults = new List<TestVariantResult>
    //     {
    //         new(new FrameworkTestVariant("v1", null), new List<ResponseWrapper> { new(200, content, null) })
    //     };
    //     var result = await validator.ValidateAsync(testResults, default);
    //     Assert.That(result.IsSuccess, Is.True);
    // }

    // [Test]
    // public async Task ValidateAsync_InvalidJsonField_ReturnsError()
    // {
    //     var validation = new FieldFormatValidation("foo", Format.Json);
    //     var validator = new FieldFormatValidator(validation);
    //     var content = JsonSerializer.Deserialize<JsonElement>("{\"foo\": \"not-json\"}");
    //     var testResults = new List<TestVariantResult>
    //     {
    //         new(new FrameworkTestVariant("v1", null), new List<ResponseWrapper> { new(200, content, null) })
    //     };
    //     var result = await validator.ValidateAsync(testResults, default);
    //     Assert.That(result.IsSuccess, Is.False);
    //     Assert.That(result.UnwrapError().Message, Does.Contain("JSON"));
    // }
}
