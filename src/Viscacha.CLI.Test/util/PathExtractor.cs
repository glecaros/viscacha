using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Viscacha.CLI.Test.Util;

internal class PathExtractor(ResponseWrapper response, bool preserveArrayIndices = false)
{
    private readonly HashSet<string> _paths = [];

    private void ExtractPaths(JsonElement element, string? path)
    {
        foreach (var property in element.EnumerateObject())
        {
            string currentPath = path == null ? property.Name : $"{path}.{property.Name}";

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                _paths.Add(currentPath);
                ExtractPaths(property.Value, currentPath);
            }
            else if (property.Value.ValueKind == JsonValueKind.Array)
            {
                _paths.Add(currentPath);
                foreach (var (index, item) in property.Value.EnumerateArray().Enumerate())
                {
                    var arrayPath = preserveArrayIndices ?
                                    $"{currentPath}[{index}]" :
                                    $"{currentPath}[]";
                    _paths.Add(arrayPath);
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        ExtractPaths(item, arrayPath);
                    }
                }
            }
            else
            {
                _paths.Add(currentPath);
            }
        }
    }

    public HashSet<string> ExtractPaths()
    {
        using var document = JsonSerializer.SerializeToDocument(response.Content);

        ExtractPaths(document.RootElement, null);

        return _paths;
    }
}
