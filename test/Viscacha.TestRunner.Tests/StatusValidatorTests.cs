using System.Collections.Generic;
using System.Threading.Tasks;
using Viscacha.Model;
using Viscacha.TestRunner.Framework;
using Viscacha.TestRunner.Framework.Validation;
using Viscacha.TestRunner.Model;

namespace Viscacha.TestRunner.Tests;

[TestFixture]
public class StatusValidatorTests
{
    [Test]
    public async Task ValidateAsync_AllStatusesMatch_ReturnsOk()
    {
        Document doc = new (Defaults.Empty, []);
        StatusValidation validation = new(200) { Target = new Target.All() };
        StatusValidator validator = new(validation);
        List<TestVariantResult> testResults = [
            new(new("v1", doc), [new(200, null, null, [])]),
            new(new("v2", doc), [new(200, null, null, [])])
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Ok);
    }

    [Test]
    public async Task ValidateAsync_StatusMismatch_ReturnsError()
    {
        Document doc = new(Defaults.Empty, []);
        StatusValidation validation = new(200) { Target = new Target.All() };
        StatusValidator validator = new(validation);
        List<TestVariantResult> testResults = [new(new FrameworkTestVariant("v1", doc), [new(404, null, null, [])])];

        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Err);
        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("does not match expected status"));
    }
}
