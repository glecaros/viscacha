using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Path;
using Viscacha.Model;

namespace Viscacha.TestRunner.Util;

internal class FieldExtractor(string path)
{
    private readonly string _path = path;

    private Result<JsonPath, Error> ParsePath()
    {
        try
        {
            return JsonPath.Parse(_path);
        }
        catch (Exception ex)
        {
            return new Error($"Failed to parse JSON path: {ex.Message}");
        }
    }

    private static Result<JsonNode, Error> ObjectToJsonNode(object obj)
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

    public Result<List<T>, Error> ExtractFields<T>(object obj)
    {
        return ParsePath().Then(path => {
            JsonSerializer.SerializeToNode(obj);
            return ObjectToJsonNode(obj).Then<List<T>>(node => {
                List<T> results = [];
                var result = path.Evaluate(node);
                foreach (var (location, value) in result.Matches)
                {
                    try
                    {
                        var deserializedValue = value.Deserialize<T>();
                        if (deserializedValue is not null)
                        {
                            results.Add(deserializedValue);
                        }
                        else
                        {
                            return new Error($"Failed to deserialize JSON node at {location}: null value.");
                        }
                    }
                    catch (JsonException ex)
                    {
                        return new Error($"Failed to deserialize JSON node: {ex.Message}");
                    }
                }
                return results;
            });
        });
        
    }

}

internal static class Extensions
{
    public static void Deconstruct(this Node node, out JsonPath? Location, out JsonNode? Value)
    {
        Location = node.Location;
        Value = node.Value;
    }
}