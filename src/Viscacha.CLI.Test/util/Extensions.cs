using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Path;
using Viscacha.Model;

namespace Viscacha.CLI.Test.Util;

internal static class EnumerableExtensions
{
    public static IEnumerable<(int Index, T Value)> Enumerate<T>(this IEnumerable<T> source)
    {
        int index = 0;
        foreach (var item in source)
        {
            yield return (index++, item);
        }
    }
}

internal static class SerializerExtensions
{
    public static Result<JsonNode, Error> ObjectToJsonNode(this object obj)
    {
        try
        {
            return JsonSerializer.SerializeToNode(obj) switch
            {
                JsonNode node => node,
                null => new Error("Failed to serialize object to JSON."),
            };
        }
        catch (Exception ex)
        {
            return new Error($"Failed to serialize object to JSON: {ex.Message}");
        }
    }

    public static void Deconstruct(this Node node, out JsonPath? Location, out JsonNode? Value)
    {
        Location = node.Location;
        Value = node.Value;
    }
}
