using System.Collections.Generic;
using System.Text.Json;

namespace Viscacha.TestRunner.Util;

internal class PathExtractor(ResponseWrapper response)
{
    private readonly HashSet<string> _paths = [];

    private void ExtractPaths(JsonElement element, string? path)
    {
        foreach (var property in element.EnumerateObject())
        {
            string currentPath = path == null ? property.Name : $"{path}.{property.Name}";

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                ExtractPaths(property.Value, currentPath);
            }
            else if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        ExtractPaths(item, currentPath);
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