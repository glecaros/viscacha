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
        Assert.That(paths, Does.Contain("foo"));
        Assert.That(paths, Does.Contain("foo.bar"));
        Assert.That(paths, Does.Contain("foo.baz"));
        Assert.That(paths, Does.Contain("qux"));
    }

    [Test]
    public void ExtractPaths_ReturnsAllPaths_ForNestedObjectWithScalarArray_AndPreserveArrayIndices()
    {
        var obj = new { foo = new { bar = 1, baz = 2 }, qux = new[] { 3, 4 } };
        var wrapper = new ResponseWrapper(200, obj, null, []);
        var extractor = new PathExtractor(wrapper, true);
        var paths = extractor.ExtractPaths();
        Assert.That(paths, Does.Contain("foo"));
        Assert.That(paths, Does.Contain("foo.bar"));
        Assert.That(paths, Does.Contain("foo.baz"));
        Assert.That(paths, Does.Contain("qux"));
        Assert.That(paths, Does.Contain("qux[0]"));
        Assert.That(paths, Does.Contain("qux[1]"));
    }

    [Test]
    public void ExtractPaths_ReturnsAllPaths_ForNestedObjectWithScalarArray_AndPreserveArrayIndicesFalse()
    {
        var obj = new { foo = new { bar = 1, baz = 2 }, qux = new[] { 3, 4 } };
        var wrapper = new ResponseWrapper(200, obj, null, []);
        var extractor = new PathExtractor(wrapper, false);
        var paths = extractor.ExtractPaths();
        Assert.That(paths, Does.Contain("foo"));
        Assert.That(paths, Does.Contain("foo.bar"));
        Assert.That(paths, Does.Contain("foo.baz"));
        Assert.That(paths, Does.Contain("qux"));
        Assert.That(paths, Does.Contain("qux[]"));
    }

    [Test]
    public void ExtractPaths_ReturnsAllPaths_ForNestedObjectWithArrayOfObjects()
    {
        var obj = new { foo = new object[] { new { bar = 1 }, new { baz = 2 } }, qux = 3 };
        var wrapper = new ResponseWrapper(200, obj, null, []);
        var extractor = new PathExtractor(wrapper);
        var paths = extractor.ExtractPaths();
        Assert.That(paths, Does.Contain("foo"));
        Assert.That(paths, Does.Contain("foo[]"));
        Assert.That(paths, Does.Contain("foo[].bar"));
        Assert.That(paths, Does.Contain("foo[].baz"));
        Assert.That(paths, Does.Contain("qux"));
    }

    [Test]
    public void ExtractPaths_ReturnsAllPaths_ForNestedObjectWithArrayOfObjects_AndPreserveArrayIndices()
    {
        var obj = new { foo = new object[] { new { bar = 1 }, new { baz = 2 } }, qux = 3 };
        var wrapper = new ResponseWrapper(200, obj, null, []);
        var extractor = new PathExtractor(wrapper, true);
        var paths = extractor.ExtractPaths();
        Assert.That(paths, Does.Contain("foo"));
        Assert.That(paths, Does.Contain("foo[0]"));
        Assert.That(paths, Does.Contain("foo[0].bar"));
        Assert.That(paths, Does.Contain("foo[1]"));
        Assert.That(paths, Does.Contain("foo[1].baz"));
        Assert.That(paths, Does.Contain("qux"));
    }


}