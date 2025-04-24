using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Viscacha.Model;
using Viscacha.Model.Test;
using Viscacha.TestRunner.Framework;
using Viscacha.TestRunner.Framework.Validation;
using Viscacha.TestRunner.Util;

namespace Viscacha.TestRunner.Tests;

[TestFixture]
public class StatusValidatorTests
{
    [Test]
    public async Task ValidateAsync_AllStatusesMatch_ReturnsOk()
    {
        var validation = new StatusValidation(200) { Target = new Target.All() };
        var validator = new StatusValidator(validation);
        var doc = new Document(Defaults.Empty, []);
        var testResults = new List<TestVariantResult>
        {
            new(new FrameworkTestVariant("v1", doc), [new(200, null, [])]),
            new(new FrameworkTestVariant("v2", doc), [new(200, null, [])])
        };
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Ok);
    }

    [Test]
    public async Task ValidateAsync_StatusMismatch_ReturnsError()
    {
        var validation = new StatusValidation(200) { Target = new Target.All() };
        var validator = new StatusValidator(validation);
        var doc = new Document(Defaults.Empty, []);
        List<TestVariantResult> testResults = [new(new FrameworkTestVariant("v1", doc), [new(404, null, [])])];
        
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.UnwrapError().Message, Does.Contain("does not match expected status"));
    }
}
