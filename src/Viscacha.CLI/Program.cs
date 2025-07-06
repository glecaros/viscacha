using dotenv.net;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

using Viscacha;
using Viscacha.Model;

DotEnv.Load();

Argument<FileInfo> fileArgument = new("file")
{
    Description = "YAML file containing the API request(s) to execute",
};

Option<FileInfo> defaultsFileOption = new("--defaults")
{
    Description = "YAML file containing the default values for the API request(s)",
    Arity = ArgumentArity.ZeroOrOne
};

Option<string[]> variableOption = new("--var")
{
    Description = "Variables to replace in the request (format: name=value)",
    AllowMultipleArgumentsPerToken = true
};

var rootCommand = new RootCommand("API Tester - Execute API requests defined in YAML files")
{
    fileArgument,
    defaultsFileOption,
    variableOption
};

rootCommand.SetAction((ParseResult parseResult) =>
{
    var file = parseResult.GetRequiredValue(fileArgument);
    var defaultsFile = parseResult.GetValue(defaultsFileOption);
    var variableArgs = parseResult.GetValue(variableOption);
    RunCommand(file, defaultsFile, variableArgs ?? []);
});

return rootCommand.Parse(args).Invoke();

[DoesNotReturn]
static T HandleError<T>(Result<T, Error>.Err error)
{
    Console.Error.WriteLine($"Error: {error.Error.Message}");
    Environment.Exit(-1);
    return default!;

}

static void RunCommand(FileInfo file, FileInfo? defaultsFile, string[] variableArgs)
{
    Dictionary<string, string> commandLineVariables = [];
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

    var parser = new DocumentParser(commandLineVariables);

    var doc = parser.FromFile(file, defaultsFile) switch {
        Result<Document, Error>.Ok success => success.Value,
        Result<Document, Error>.Err error => HandleError(error),
        _ => throw new InvalidOperationException("Unexpected result type") // Unreachable
    };

    var executor = new RequestExecutor(doc.Defaults);

    List<ResponseWrapper> responses = [];
    foreach (var request in doc.Requests)
    {
        var response = executor.Execute(request, doc.Requests.IndexOf(request)) switch {
            Result<ResponseWrapper, Error>.Ok success => success.Value,
            Result<ResponseWrapper, Error>.Err error => HandleError(error),
            _ => throw new InvalidOperationException("Unexpected result type") // Unreachable
        };
        if (response != null)
        {
            responses.Add(response);
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
