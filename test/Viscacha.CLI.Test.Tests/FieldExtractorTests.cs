using System.Collections.Generic;
using Viscacha.CLI.Test.Util;
using Viscacha.Model;

namespace Viscacha.CLI.Test.Tests;

[TestFixture]
public class FieldExtractorTests
{
    [Test]
    public void ExtractFields_ReturnsExpectedValues_ForSimplePath()
    {
        var obj = new { foo = new { bar = 42 } };
        var extractor = new FieldExtractor("$.foo.bar");
        var result = extractor.ExtractFields<int>(obj);
        Assert.That(result is Result<List<int>, Error>.Ok);

        var extractedFields = result.Unwrap();

        Assert.That(extractedFields, Has.Count.EqualTo(1));
        Assert.That(extractedFields[0], Is.EqualTo(42));
    }

    [Test]
    public void ExtractFields_ReturnsError_ForInvalidPath()
    {
        var obj = new { foo = 123 };
        var extractor = new FieldExtractor("$.foo[");
        var result = extractor.ExtractFields<int>(obj);

        Assert.That(result is Result<List<int>, Error>.Err);

        var error = result.UnwrapError();

        Assert.That(error.Message, Does.Contain("Failed to parse JSON path"));
    }
}