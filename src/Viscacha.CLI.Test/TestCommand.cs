using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Viscacha.CLI.Test.Framework;

namespace Viscacha.CLI.Test;

public static class TestCommand
{
    public static Command Create()
    {
        Option<FileInfo> inputFileOption = new("--input-file", ["-i"])
        {
            Required = true,
            Description = "The YAML file containing the test definitions.",
            Arity = ArgumentArity.ExactlyOne
        };
        Option<DirectoryInfo> responsesDirectoryOption = new("--responses-directory", ["-r"])
        {
            Required = false,
            Description = "If set, responses will be saved to this directory.",
            Arity = ArgumentArity.ExactlyOne
        };
        Option<bool> listTestsOption = new("--list-tests")
        {
            Required = false,
            Description = "List available tests.",
            Arity = ArgumentArity.ZeroOrOne
        };
        Option<bool> reportTrxOption = new("--report-trx")
        {
            Required = false,
            Description = "Enable generating TRX report.",
            Arity = ArgumentArity.ZeroOrOne
        };
        Option<FileInfo> reportTrxFilenameOption = new("--report-trx-filename")
        {
            Required = false,
            Description = "The filename for the TRX report.",
            Arity = ArgumentArity.ExactlyOne
        };
        Command command = new("test", "Run tests defined in a YAML file.")
        {
            inputFileOption,
            responsesDirectoryOption,
            listTestsOption,
            reportTrxOption,
            reportTrxFilenameOption
        };
        command.SetAction(async (parseResult) =>
        {
            List<string> args = [];
            var inputFile = parseResult.GetRequiredValue(inputFileOption);
            args.AddRange([inputFileOption.Name, inputFile.FullName]);
            if (parseResult.GetValue(responsesDirectoryOption) is DirectoryInfo responsesDirectory)
            {
                args.AddRange([responsesDirectoryOption.Name, responsesDirectory.FullName]);
            }
            if (parseResult.GetValue(listTestsOption))
            {
                args.Add(listTestsOption.Name);
            }
            if (parseResult.GetValue(reportTrxOption))
            {
                args.Add(reportTrxOption.Name);
            }
            if (parseResult.GetValue(reportTrxFilenameOption) is FileInfo reportTrxFile)
            {
                args.AddRange([reportTrxFilenameOption.Name, reportTrxFile.FullName]);
            }

            var builder = await TestApplication.CreateBuilderAsync([.. args]);

            builder.RegisterTestFramework(
                (_) => new TestingFrameworkCapabilities(),
                (_, serviceProvider) => new TestingFramework(serviceProvider));
            builder.CommandLine.AddProvider(() => new CommandLineOptions());
            builder.AddTrxReportProvider();
            using var application = await builder.BuildAsync();
            var resultCode = await application.RunAsync();
            Environment.Exit(resultCode);
        });
        command.TreatUnmatchedTokensAsErrors = false;
        return command;
    }
}
