using dotenv.net;
using YAYL;
using ApiTester.Models;
using ApiTester;
using System.CommandLine;
using System.CommandLine.Parsing;

DotEnv.Load();

var fileArgument = new Argument<FileInfo>(
    name: "file",
    description: "YAML file containing the API request(s) to execute");

var variableOption = new Option<string[]>(
    name: "--var",
    description: "Variables to replace in the request (format: name=value)")
{
    AllowMultipleArgumentsPerToken = true
};

var rootCommand = new RootCommand("API Tester - Execute API requests defined in YAML files")
{
    fileArgument,
    variableOption
};

rootCommand.SetHandler(RunCommand, fileArgument, variableOption);

return rootCommand.Invoke(args);

static void RunCommand(FileInfo file, string[] variableArgs)
{
    if (!file.Exists)
    {
        Console.Error.WriteLine($"File not found: {file.FullName}");
        return;
    }

    Dictionary<string, string> commandLineVariables = new();
    foreach (var varArg in variableArgs)
    {
        var parts = varArg.Split('=', 2);
        if (parts.Length == 2)
        {
            commandLineVariables[parts[0]] = parts[1];
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

    using var executor = new RequestExecutor(doc.Defaults, commandLineVariables);

    foreach (var request in doc.Requests)
    {
        try
        {
            var response = executor.Execute(request, doc.Requests.IndexOf(request));
            if (response != null)
            {
                Console.WriteLine(response);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"HTTP request failed: {ex.Message}");
        }
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
}

