using System;
using System.Collections.Generic;
using System.Linq;
using Viscacha.TestRunner.Framework;

namespace Viscacha.TestRunner.Util;

internal record ResponseGroup(List<ResponseEntry> Entries);

internal record ResponseEntry(string Variant, ResponseWrapper Response);


internal class ResponseGrouper
{
    internal static List<ResponseGroup> GroupResponsesByRequestIndex(ReadOnlySpan<TestVariantResult> testResults)
    {
        return testResults switch
        {
            [] => [],
            [var result] => [.. result.Responses.Select(response => new ResponseGroup([ new(result.Variant.Name, response) ]))],
            [var result, .. var rest] => ZipGroups(GroupResponsesByRequestIndex(rest), result.Responses, result.Variant.Name),
        };
    }

    private static List<ResponseGroup> ZipGroups(List<ResponseGroup> groups, List<ResponseWrapper> responses, string variantName)
    {
        return [.. groups.Zip(responses, (group, response) =>
        {
            group.Entries.Add(new(variantName, response));
            return group;
        })];
    }
}