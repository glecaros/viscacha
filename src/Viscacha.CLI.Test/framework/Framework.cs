using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Viscacha.Model;

namespace Viscacha.CLI.Test.Framework;

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

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    private readonly ICommandLineOptions _commandLineOptions;
    private readonly Dictionary<SessionUid, Session> _sessions = [];

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
    }
    public async Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        _commandLineOptions.TryGetOptionArgumentList(CommandLineOptions.InputFileOption, out var inputFileArguments);
        if (inputFileArguments is not [string inputFile])
        {
            return new()
            {
                ErrorMessage = $"The {CommandLineOptions.InputFileOption} option is required.",
                IsSuccess = false,
            };
        }
        _commandLineOptions.TryGetOptionArgumentList(CommandLineOptions.ResponsesDirectoryOption, out var responsesDirectoryArguments);
        var responsesDirectory = responsesDirectoryArguments switch {
            [string path] => new DirectoryInfo(path),
            _ => null
        };

        var session = new Session(context.SessionUid, new(new(inputFile), responsesDirectory));
        if (await session.InitAsync(context.CancellationToken).ConfigureAwait(false) is Result<Error>.Err error)
        {
            return new()
            {
                ErrorMessage = error.Error.Message,
                IsSuccess = false,
            };
        }
        _sessions.Add(session.Uid, session);
        return new()
        {
            IsSuccess = true
        };
    }

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        _sessions.Remove(context.SessionUid);
        return Task.FromResult(new CloseTestSessionResult{ IsSuccess = true });
    }


    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        _sessions.TryGetValue(context.Request.Session.SessionUid, out var session);
        if (session == null)
        {
            throw new InvalidOperationException("Session not found.");
        }
        switch (context.Request)
        {
            case DiscoverTestExecutionRequest _:
                try
                {
                    await session.DiscoverTestsAsync(this, context, context.CancellationToken).ConfigureAwait(false);

                }
                finally
                {
                    context.Complete();
                }
                break;
            case RunTestExecutionRequest _:
                try
                {
                    await session.RunTestsAsync(this, context, context.CancellationToken).ConfigureAwait(false);

                }
                finally
                {
                    context.Complete();
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported request type: {context.Request.GetType().Name}");
        }
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