using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.OutputDevice;

namespace Viscacha.TestRunner.Framework;

internal sealed class DisplayDataConsumer : IDataConsumer, IOutputDeviceDataProducer
{
    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(DisplayDataConsumer);

    private string? _version;
    public string Version => _version ??= this.GetVersion();

    public string DisplayName => nameof(DisplayDataConsumer);

    public string Description => "Displays test results in the console.";

    private readonly IOutputDevice _outputDevice;

    public DisplayDataConsumer(IOutputDevice outputDevice)
    {
        _outputDevice = outputDevice;
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        switch (value)
        {
            case TestNodeUpdateMessage testNodeUpdateMessage:
                var displayName = testNodeUpdateMessage.TestNode.DisplayName;
                var testNodeId = testNodeUpdateMessage.TestNode.Uid;

                var  nodeState = testNodeUpdateMessage.TestNode.Properties.Single<TestNodeStateProperty>();
                switch (nodeState)
                {
                    case InProgressTestNodeStateProperty _:
                        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"{displayName} ({testNodeId}) is in progress.")
                        {
                            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Yellow },
                        });
                        break;
                    case PassedTestNodeStateProperty _:
                        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"{displayName} ({testNodeId}) passed.")
                        {
                            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green },
                        });
                        break;
                    case FailedTestNodeStateProperty failedTestNodeStateProperty:
                        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"{displayName} ({testNodeId}) failed: {failedTestNodeStateProperty.Explanation}")
                        {
                            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Red },
                        });
                        break;
                    case SkippedTestNodeStateProperty _:
                        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"{displayName} ({testNodeId}) was skipped.")
                        {
                            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Yellow },
                        });
                        break;
                    case DiscoveredTestNodeStateProperty _:
                        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData($"{displayName} ({testNodeId}) was discovered.")
                        {
                            ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Yellow },
                        });
                        break;
                }
                break;
        }
    }

    public Task<bool> IsEnabledAsync()
    {
        throw new NotImplementedException();
    }
}