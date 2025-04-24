using System.Collections.Generic;
using System.Threading.Tasks;
using Viscacha.Model;
using Viscacha.Model.Test;
using Viscacha.TestRunner.Framework;
using Viscacha.TestRunner.Framework.Validation;

namespace Viscacha.TestRunner.Tests;

[TestFixture]
public class PathComparisonValidatorTests
{
    [Test]
    public async Task ValidateAsync_BaselineAndVariantMatch_ReturnsOk()
    {
        Document doc = new(Defaults.Empty, []);
        PathComparisonValidation validation = new("baseline");
        PathComparisonValidator validator = new (validation);
        var baselineContent = new{ a = 1, b = 2 };
        var variantContent = new{ a = 1, b = 2 };         
        List<TestVariantResult> testResults = [
            new(new("baseline", doc), [new(200, baselineContent, [])]),
            new(new ("other", doc), [new(200, variantContent, [])]),
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Ok);
    }

    [Test]
    public async Task ValidateAsync_MissingPathInVariant_ReturnsError()
    {
        Document doc = new(Defaults.Empty, []);
        var validation = new PathComparisonValidation("baseline");
        var validator = new PathComparisonValidator(validation);
        var baselineContent = new{ a = 1, b = 2 };
        var variantContent = new{ a = 1 };
        List<TestVariantResult> testResults = [
            new(new("baseline", doc), [new(200, baselineContent, [])]),
            new(new("other", doc), [new(200, variantContent, [])]),
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Err);
        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("missing paths"));        
    }
}
