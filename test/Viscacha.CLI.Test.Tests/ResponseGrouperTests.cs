using Viscacha.CLI.Test.Framework;
using Viscacha.CLI.Test.Util;

namespace Viscacha.CLI.Test.Tests;

[TestFixture]
public class ResponseGrouperTests
{

    [Test]
    public void GroupResponsesByRequestIndex_GroupsCorrectly_ForSingleVariant()
    {
        ResponseWrapper response = new(
            200,
            new { foo = "bar" },
            null,
            new()
            {
                ["header1"] = ["value1"],
            }
        );
        var result = new TestVariantResult(
            new("variant", new(null, [])),
            [response]
        );
        var groups = ResponseGrouper.GroupResponsesByRequestIndex([result]);
        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].Entries, Has.Count.EqualTo(1));
        Assert.That(groups[0].Entries[0].Variant, Is.EqualTo("variant"));
    }
}
