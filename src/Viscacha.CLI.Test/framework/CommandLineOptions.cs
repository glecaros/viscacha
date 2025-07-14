using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Viscacha.CLI.Test.Framework;

internal sealed class CommandLineOptions : ICommandLineOptionsProvider
{
    public const string InputFileOption = "input-file";
    public const string ResponsesDirectoryOption = "responses-directory";

    public string Uid => nameof(CommandLineOptions);

    private string? _version;
    public string Version => _version ??= this.GetVersion();

    public string DisplayName => nameof(CommandLineOptions);

    public string Description => nameof(CommandLineOptions);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() => [
        new (InputFileOption, "Path to the input file", ArgumentArity.ExactlyOne, false),
        new (ResponsesDirectoryOption, "If set, responses will be saved to this directory", ArgumentArity.ExactlyOne, false),
    ];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if (commandLineOptions.IsOptionSet("help"))
        {
            return ValidationResult.ValidTask;
        }
        bool hasInputFileOption = commandLineOptions.IsOptionSet(InputFileOption);
        if (!hasInputFileOption)
        {
            return ValidationResult.InvalidTask($"The {InputFileOption} option is required.");
        }
        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == InputFileOption)
        {
            if (arguments.Length != 1)
            {
                return ValidationResult.InvalidTask($"The {InputFileOption} option requires exactly one argument.");
            }
            var filePath = arguments[0];
            if (!File.Exists(filePath))
            {
                return ValidationResult.InvalidTask($"The file {filePath} does not exist.");
            }
            if (!filePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) && !filePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask($"The file {filePath} is not a valid YAML file.");
            }
        }
        else if (commandOption.Name == ResponsesDirectoryOption)
        {
            if (arguments.Length != 1)
            {
                return ValidationResult.InvalidTask($"The {ResponsesDirectoryOption} option requires exactly one argument.");
            }
            var directoryPath = arguments[0];
            DirectoryInfo directoryInfo = new(directoryPath);
            if (directoryInfo.Exists)
            {
                return ValidationResult.ValidTask;
            }
            try
            {
                directoryInfo.Create();
            }
            catch (Exception ex)
            {
                return ValidationResult.InvalidTask($"Failed to create directory {directoryPath}: {ex.Message}");
            }
        }
        else
        {
            return ValidationResult.InvalidTask($"Unknown option: {commandOption.Name}");
        }
        return ValidationResult.ValidTask;
    }
}
