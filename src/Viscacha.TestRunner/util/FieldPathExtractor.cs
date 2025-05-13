using System;
using System.Collections.Generic;
using System.Text.Json;
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

    public Result<List<T>, Error> ExtractFields<T>(object obj)
    {
        return ParsePath().Then(path => {
            return obj.ObjectToJsonNode().Then<List<T>>(node => {
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
