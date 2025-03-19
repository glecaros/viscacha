using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

namespace Viscacha.TestRunner.Framework;

internal sealed class TestingFrameworkCapabilities : ITestFrameworkCapabilities
{
    public TrxCapability TrxCapability { get; } = new();

    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => [TrxCapability];
}

internal sealed class TrxCapability : ITrxReportCapability
{
    public bool IsSupported => true;

    public bool Enabled { get; set; }

    public void Enable() => Enabled = true;
}

internal sealed class TestingFramework : ITestFramework, IDataProducer, IDisposable, IOutputDeviceDataProducer
{
    public string Uid => nameof(TestingFramework);

    private string? _version;
    public string Version => _version ??= this.GetVersion();

    public string DisplayName => "viscacha-test";

    public string Description => "Runner for tests written with Viscacha files.";

    public Type[] DataTypesProduced => throw new NotImplementedException();

    private readonly ICommandLineOptions _commandLineOptions;

    public TestingFramework(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<ICommandLineOptions>() is ICommandLineOptions commandLineOptions)
        {
            _commandLineOptions = commandLineOptions;
        }
        else
        {
            throw new InvalidOperationException("Command line options not found.");
        }
        // Initialize any services or dependencies here
    }

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        return Task.FromResult(new CloseTestSessionResult{ IsSuccess = true });
    }

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        return Task.FromResult(new CreateTestSessionResult{ IsSuccess = true });
    }

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        _commandLineOptions.TryGetOptionArgumentList(CommandLineOptions.InputFileOption, out var inputFileArguments);
        if (inputFileArguments is not [string inputFile])
        {
            throw new InvalidOperationException($"The {CommandLineOptions.InputFileOption} option is required.");
        }
        Console.WriteLine($"Input file: {inputFile}");

        Console.WriteLine(context.Request.ToString());

        return Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public void Dispose()
    {
    }
}

internal static class Extensions
{
    public static string GetVersion<T>(this T obj)
    {
        var assembly = typeof(T).Assembly;
        var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
        return versionInfo.FileVersion ?? "unknown";
    }
}