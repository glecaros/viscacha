using System;
using System.Collections.Generic;
using System.IO;
using YAYL;
using Viscacha.Model;
using System.Threading.Tasks;
using System.Threading;

namespace Viscacha;

public class DocumentParser
{
    private readonly YamlParser _parser;
    private readonly Dictionary<string, string> _variables;

    public DocumentParser(Dictionary<string, string> variables)
    {
        _parser = new YamlParser();
        _parser.AddVariableResolver(Constants.EnvironmentVariableRegex, ResolveVariableValue);
        _variables = variables;
    }

    private string ResolveVariableValue(string variableName)
    {
        if (variableName.StartsWith("env:"))
        {
            return Environment.GetEnvironmentVariable(variableName[4..]) ?? string.Empty;
        }

        if (_variables.TryGetValue(variableName, out var value))
        {
            return value;
        }
        return string.Empty;
    }

    public Result<Document, Error> FromFile(FileInfo file, FileInfo? defaultsFile)
    {
        return FromFileAsync(file, defaultsFile).GetAwaiter().GetResult();
    }

    public async Task<Result<Document, Error>> FromFileAsync(FileInfo file, FileInfo? defaultsFile, CancellationToken cancellationToken = default)
    {
        if (!file.Exists)
        {
            return new Error($"File not found: {file.FullName}");
        }

        if (defaultsFile is not null && !defaultsFile.Exists)
        {
            return new Error($"Defaults file not found: {defaultsFile.FullName}");
        }

        Document? doc;
        if (await _parser.TryParseFileAsync<Document>(file.FullName, cancellationToken).ConfigureAwait(false) is Result<Document?, Error>.Ok documentResult and { Value: not null})
        {
            doc = documentResult.Value;
        }
        else if (await _parser.TryParseFileAsync<Request>(file.FullName, cancellationToken).ConfigureAwait(false) is Result<Request?, Error>.Ok requestResult and { Value: not null })
        {
            doc = new Document(Defaults.Empty, new List<Request> { requestResult.Value });
        }
        else
        {
            return new Error("Failed to parse YAML as either document type");
        }

        Defaults? extraDefaults = null;
        if (defaultsFile is not null)
        {
            if (await _parser.TryParseFileAsync<Defaults>(defaultsFile.FullName, cancellationToken).ConfigureAwait(false) is Result<Defaults?, Error>.Ok defaultsResult and { Value: not null })
            {
                extraDefaults = defaultsResult.Value;
            }
            else
            {
                return new Error("Failed to parse defaults file");
            }
        }

        Defaults? importedDefaults = null;
        if (doc.Defaults?.Import is string importFile)
        {
            var importPath = Path.Combine(file.Directory?.FullName ?? ".", importFile);
            if (await _parser.TryParseFileAsync<Defaults>(importPath, cancellationToken).ConfigureAwait(false) is Result<Defaults?, Error>.Ok importResult and { Value: not null })
            {
                importedDefaults = importResult.Value;
            }
            else
            {
                return new Error("Failed to parse imported defaults file");
            }
        }

        if (extraDefaults is not null || importedDefaults is not null)
        {
            doc = new Document(
                new Defaults(
                    doc.Defaults?.Import,
                    extraDefaults?.BaseUrl ?? doc.Defaults?.BaseUrl ?? importedDefaults?.BaseUrl,
                    extraDefaults?.Authentication ?? doc.Defaults?.Authentication ?? importedDefaults?.Authentication,
                    (extraDefaults?.Headers).Merge((doc.Defaults?.Headers).Merge(importedDefaults?.Headers)),
                    (extraDefaults?.Query).Merge((doc.Defaults?.Query).Merge(importedDefaults?.Query)),
                    extraDefaults?.ContentType ?? doc.Defaults?.ContentType ?? importedDefaults?.ContentType
                ),
                doc.Requests
            );
        }
        return doc;
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
            var result = await parser.ParseFileAsync<T>(filePath, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (YamlParseException e)
        {
            return new Error(e.Message);
        }
    }
}

public static class Util
{
    public static Dictionary<TKey, TValue>? Merge<TKey, TValue>(this Dictionary<TKey, TValue>? left, Dictionary<TKey, TValue>? right) where TKey : notnull
    {
        if (left is null)
        {
            return right;
        }
        if (right is null)
        {
            return left;
        }
        foreach (var (key, value) in right)
        {
            left[key] = value;
        }
        return left;
    }
}

