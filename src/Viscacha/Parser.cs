using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.Model;
using YAYL;

namespace Viscacha;

public class Parser
{
    public Parser(Dictionary<string, string>? variables = null, DirectoryInfo? workingDirectory = null)
    {
        _parser = new YamlParser();
        _parser.AddVariableResolver(Constants.VariableRegex, ResolveVariableValue);
        _variables = variables ?? [];
        _workingDirectory = workingDirectory ?? new(Directory.GetCurrentDirectory());
    }

    protected readonly YamlParser _parser;
    private readonly Dictionary<string, string> _variables;
    private readonly DirectoryInfo _workingDirectory;

    private Result<string, Error> GetFileValue(string fileName, string? format)
    {
        var fullPath = Path.Combine(_workingDirectory.FullName, fileName);
        if (!File.Exists(fullPath))
        {
            return new Error($"File not found: {fileName}");
        }
        try
        {
            var binaryContent = File.ReadAllBytes(fullPath);
            if (binaryContent.Length == 0)
            {
                return new Error($"File is empty: {fileName}");
            }
            return format switch
            {
                "base64" => Convert.ToBase64String(binaryContent),
                _ => new Error($"Unsupported format: {format}")
            };
        }
        catch (Exception ex)
        {
            return new Error($"Failed to read file: {fileName}. Error: {ex.Message}");
        }

    }

    private string ResolveVariableValue(string variableName)
    {
        if (variableName.StartsWith("env:"))
        {
            return Environment.GetEnvironmentVariable(variableName[4..]) ?? string.Empty;
        }
        if (variableName.StartsWith("file:") && variableName.Split(':', 3) is [_, var fileName, var format])
        {
            var result = GetFileValue(fileName, format);
            if (result is Result<string, Error>.Ok { Value: var fileData })
            {
                return fileData;
            }
            else
            {
                var error = result.UnwrapError();
                Console.Error.WriteLine($"Error resolving variable '{variableName}': {error.Message}");
                return string.Empty;
            }
        }

        if (_variables.TryGetValue(variableName, out var value))
        {
            return value;
        }
        return string.Empty;
    }

    public async Task<Result<T, Error>> TryParseFileAsync<T>(string filePath, CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return new Error("File path cannot be null or empty");
        }

        var result = await _parser.TryParseFileAsync<T>(filePath, cancellationToken)
                                  .ConfigureAwait(false);
        if (result is Result<T?, Error>.Err errorResult)
        {
            return errorResult.UnwrapError();
        }
        else  if (result is Result<T?, Error>.Ok { Value: not null } okResult)
        {
            return okResult.Value;
        }
        else
        {
            return new Error($"Failed to parse file: {filePath}");
        }
    }

    public Result<T, Error> TryParseFile<T>(string filePath) where T : class
    {
        return TryParseFileAsync<T>(filePath).GetAwaiter().GetResult();
    }

}

internal static class YamlExtensions
{
    public static bool TryParseFile<T>(this YamlParser parser, string filePath, out T? result) where T : class
    {
        try
        {
            result = parser.ParseFile<T>(filePath);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    public static async Task<Result<T?, Error>> TryParseFileAsync<T>(this YamlParser parser, string filePath, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var result = await parser.ParseFileAsync<T>(filePath, null, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (YamlParseException e)
        {
            return new Error(e.Message);
        }
    }
}
