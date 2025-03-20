using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Viscacha.TestRunner;
using Viscacha.TestRunner.Framework;
args = ["--input-file", "/workspaces/api-tester/test-schema.yaml", "--list-tests"];
var builder = await TestApplication.CreateBuilderAsync(args);

builder.RegisterTestFramework(
    (_) => new TestingFrameworkCapabilities(),
    (_, serviceProvider) => new TestingFramework(serviceProvider));
builder.CommandLine.AddProvider(() => new CommandLineOptions());

// builder.TestHost.AddDataConsumer((serviceProvider) => new DisplayDataConsumer(serviceProvider.GetService<IOutputDevice>()!));
var application = await builder.BuildAsync();

return await application.RunAsync();