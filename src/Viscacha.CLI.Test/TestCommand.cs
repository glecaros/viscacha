using Microsoft.Testing.Platform.Builder;
using Viscacha.CLI.Test.Framework;
using Microsoft.Testing.Extensions;
using System;
using System.CommandLine;
using System.Linq;

namespace Viscacha.CLI.Test;

public static class TestCommand
{
    public static Command Create()
    {
        Command command = new("test", "Run tests defined in a YAML file.")
        {
            new Option<bool>("--help")
            {
                Hidden = true
            }
        };
        command.SetAction(async (parseResult) =>
        {
            var args = parseResult.UnmatchedTokens.ToArray();
            /* TODO: We need to map the options we want exposed and let the command line library handle the rest. */
            if (parseResult.GetValue<bool>("--help"))
            {
                args = ["--help"];
            }

            var builder = await TestApplication.CreateBuilderAsync(args);

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

