using Viscacha.TestRunner.Util;

namespace Viscacha.TestRunner.Tests;

[TestFixture]
public class PathExtractorTests
{
    [Test]
    public void ExtractPaths_ReturnsAllPaths_ForNestedObject()
    {
        var obj = new { foo = new { bar = 1, baz = 2 }, qux = 3 };
        var wrapper = new ResponseWrapper(200, obj, null, []);
        var extractor = new PathExtractor(wrapper);
        var paths = extractor.ExtractPaths();
        Assert.That(paths, Does.Contain("foo.bar"));
        Assert.That(paths, Does.Contain("foo.baz"));
        Assert.That(paths, Does.Contain("qux"));
    }
}