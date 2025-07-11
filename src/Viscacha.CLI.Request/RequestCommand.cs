using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using Viscacha.CLI.Common;

namespace Viscacha.CLI.Request;

public static class RequestCommand
{
    public static Command Create()
    {
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

        var command = new Command("request", "Execute API requests defined in YAML files")
        {
            fileArgument,
            defaultsFileOption,
            variableOption
        };

        command.SetAction((ParseResult parseResult) =>
        {
            var file = parseResult.GetRequiredValue(fileArgument);
            var defaultsFile = parseResult.GetValue(defaultsFileOption);
            var variableArgs = parseResult.GetValue(variableOption);
            RunCommand(file, defaultsFile, variableArgs ?? []);
        });

        return command;
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

        var doc = parser.FromFile(file, defaultsFile).UnwrapOrExit();

        var executor = new RequestExecutor(doc.Defaults);

        List<ResponseWrapper> responses = [];
        foreach (var request in doc.Requests)
        {
            var response = executor.Execute(request, doc.Requests.IndexOf(request)).UnwrapOrExit();
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


}
