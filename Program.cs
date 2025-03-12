using dotenv.net;
using YAYL;
using ApiTester.Models;
using ApiTester;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;

DotEnv.Load();

var fileArgument = new Argument<FileInfo>(
    name: "file",
    description: "YAML file containing the API request(s) to execute");

var defaultsFile = new Option<FileInfo>(
    name: "--defaults",
    description: "YAML file containing the default values for the API request(s)")
{
    Arity = ArgumentArity.ZeroOrOne
};

var variableOption = new Option<string[]>(
    name: "--var",
    description: "Variables to replace in the request (format: name=value)")
{
    AllowMultipleArgumentsPerToken = true
};

var rootCommand = new RootCommand("API Tester - Execute API requests defined in YAML files")
{
    fileArgument,
    defaultsFile,
    variableOption
};

rootCommand.SetHandler(RunCommand, fileArgument, defaultsFile, variableOption);

return rootCommand.Invoke(args);

static void RunCommand(FileInfo file, FileInfo? defaultsFile, string[] variableArgs)
{
    if (!file.Exists)
    {
        Console.Error.WriteLine($"File not found: {file.FullName}");
        Environment.Exit(-1);
    }

    if (defaultsFile is not null && !defaultsFile.Exists)
    {
        Console.Error.WriteLine($"Defaults file not found: {defaultsFile.FullName}");
        Environment.Exit(-1);
    }

    Dictionary<string, string> commandLineVariables = new();
    foreach (var varArg in variableArgs)
    {
        if (varArg.Split('=', 2) is [var variableName, var variableValue])
        {
            commandLineVariables[variableName] = ResolveVariableValue(variableValue);
        }
        else
        {
            Console.Error.WriteLine($"Warning: Ignoring invalid variable format: {varArg}. Expected format: name=value");
        }
    }

    var parser = new YamlParser();
    parser.AddVariableResolver(Constants.EnvironmentVariableRegex, variable => Environment.GetEnvironmentVariable(variable) ?? "");

    Document? doc = null;
    if (parser.TryParseFile<Document>(file.FullName, out var document) && document is not null)
    {
        doc = document;
    }
    else if (parser.TryParseFile<Request>(file.FullName, out var request) && request is not null)
    {
        doc = new Document(Defaults.Empty, new List<Request> { request });
    }
    else
    {
        Console.Error.WriteLine("Failed to parse YAML as either document type");
        Environment.Exit(-1);
    }

    Defaults? commandLineDefaults = null;
    if (defaultsFile is not null)
    {
        if (!parser.TryParseFile<Defaults>(defaultsFile.FullName, out commandLineDefaults) || commandLineDefaults is null)
        {
            Console.Error.WriteLine("Failed to parse defaults file");
            Environment.Exit(-1);
        }
    }

    Defaults? importedDefaults = null;
    if (doc.Defaults?.Import is string importFile)
    {
        var importPath = Path.Combine(file.Directory?.FullName ?? ".", importFile);
        if (!parser.TryParseFile(importPath, out importedDefaults) || importedDefaults is null)
        {
            Console.Error.WriteLine("Failed to parse imported defaults file");
            Environment.Exit(-1);
        }
    }

    if (commandLineDefaults is not null || importedDefaults is not null)
    {
        doc = new Document(
            new Defaults(
                doc.Defaults?.Import,
                commandLineDefaults?.BaseUrl ?? doc.Defaults?.BaseUrl ?? importedDefaults?.BaseUrl,
                commandLineDefaults?.Authentication ?? doc.Defaults?.Authentication ?? importedDefaults?.Authentication,
                commandLineDefaults?.Headers.Merge(doc.Defaults?.Headers.Merge(importedDefaults?.Headers)),
                commandLineDefaults?.Query.Merge(doc.Defaults?.Query.Merge(importedDefaults?.Query)),
                commandLineDefaults?.ContentType ?? doc.Defaults?.ContentType ?? importedDefaults?.ContentType
            ),
            doc.Requests
        );
    }

    using var executor = new RequestExecutor(doc.Defaults, commandLineVariables);

    List<ResponseWrapper> responses = new();
    foreach (var request in doc.Requests)
    {
        try
        {
            var response = executor.Execute(request, doc.Requests.IndexOf(request));
            if (response != null)
            {
                responses.Add(response);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"HTTP request failed: {ex.Message}");
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(responses, new JsonSerializerOptions()
    {
        WriteIndented = true,
    }));
}

static string ResolveVariableValue(string value)
{
    if (!value.StartsWith("@"))
    {
        return value;
    }
    var fileName = value[1..];
    if (!File.Exists(fileName))
    {
        Console.Error.WriteLine($"File not found: {fileName}");
        Environment.Exit(-1);
    }
    var binaryContent = File.ReadAllBytes(fileName);
    return Convert.ToBase64String(binaryContent);
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

