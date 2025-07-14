using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.Model;

namespace Viscacha;

public class DocumentParser(Dictionary<string, string> variables, DirectoryInfo? workingDirectory = null)
{
    private readonly Parser _parser = new(variables, workingDirectory);

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
        if (await _parser.TryParseFileAsync<Document>(file.FullName, cancellationToken).ConfigureAwait(false) is Result<Document?, Error>.Ok { Value: not null } documentResult)
        {
            doc = documentResult.Value;
        }
        else if (await _parser.TryParseFileAsync<Request>(file.FullName, cancellationToken).ConfigureAwait(false) is Result<Request?, Error>.Ok { Value: not null } requestResult)
        {
            doc = new Document(Defaults.Empty, [requestResult.Value]);
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

public static class Util
{
    public static Dictionary<TKey, TValue>? Merge<TKey, TValue>(this Dictionary<TKey, TValue>? left, Dictionary<TKey, TValue>? right) where TKey : notnull
    {
        if (left is null)
        {
            return right is null ? null : new(right);
        }
        if (right is null)
        {
            return left is null ? null : new(left);
        }

        Dictionary<TKey, TValue> merged = new(left);
        foreach (var (key, value) in right)
        {
            merged[key] = value;
        }
        return merged;
    }
}
