using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework.Internal;
using Viscacha.Model;
using Viscacha.CLI.Test.Framework;
using Viscacha.CLI.Test.Framework.Validation;
using Viscacha.CLI.Test.Model;

namespace Viscacha.CLI.Test.Tests;

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
            new(new("baseline", doc), [new(200, baselineContent, "application/json", [])]),
            new(new ("other", doc), [new(200, variantContent, "application/json", [])]),
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Ok);
    }

    [Test]
    public async Task ValidateAsync_BaselineObjectNullVariantNotNull_ReturnsOk()
    {
        Document doc = new(Defaults.Empty, []);
        PathComparisonValidation validation = new("baseline");
        PathComparisonValidator validator = new (validation);
        var baselineContent = new{ a = 1, b = (object?) null };
        var variantContent = new{ a = 1, b = new{ c = 3, d = 4 } };
        List<TestVariantResult> testResults = [
            new(new("baseline", doc), [new(200, baselineContent, "application/json", [])]),
            new(new ("other", doc), [new(200, variantContent, "application/json", [])]),
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
        var baselineContent = new{ a = 1, b = 2, c = 3 };
        var variantContent1 = new{ a = 1, b = 2 };
        var variantContent2 = new{ a = 1 };
        List<TestVariantResult> testResults = [
            new(new("baseline", doc), [new(200, baselineContent, "application/json", [])]),
            new(new("other1", doc), [new(200, variantContent1, "application/json", [])]),
            new(new("other2", doc), [new(200, variantContent2, "application/json", [])]),
        ];
        var result = await validator.ValidateAsync(testResults, default);
        Assert.That(result is Result<Error>.Err);
        var error = result.UnwrapError();
        Assert.That(error.Message, Does.Contain("missing paths"));
        Assert.That(error.Message, Does.Contain("other1"));
        Assert.That(error.Message, Does.Contain("other2"));
    }
}
