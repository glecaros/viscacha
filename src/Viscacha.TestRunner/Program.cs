using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;

using Viscacha.TestRunner;
using Viscacha.TestRunner.Framework;

var builder = await TestApplication.CreateBuilderAsync(args);

builder.RegisterTestFramework(
    (_) => new TestingFrameworkCapabilities(),
    (_, serviceProvider) => new TestingFramework(serviceProvider));
builder.CommandLine.AddProvider(() => new CommandLineOptions());
var application = await builder.BuildAsync();

return await application.RunAsync();