using Microsoft.Testing.Platform.Builder;
using Viscacha.TestRunner.Framework;

var builder = await TestApplication.CreateBuilderAsync(args);

builder.RegisterTestFramework(
    (_) => new TestingFrameworkCapabilities(),
    (_, serviceProvider) => new TestingFramework(serviceProvider));
builder.CommandLine.AddProvider(() => new CommandLineOptions());

// builder.TestHost.AddDataConsumer((serviceProvider) => new DisplayDataConsumer(serviceProvider.GetService<IOutputDevice>()!));
var application = await builder.BuildAsync();

return await application.RunAsync();