using Microsoft.Testing.Platform.Builder;
using dotenv.net;
using Viscacha.TestRunner.Framework;
using Microsoft.Testing.Extensions;

DotEnv.Load();

var builder = await TestApplication.CreateBuilderAsync(args);

builder.RegisterTestFramework(
    (_) => new TestingFrameworkCapabilities(),
    (_, serviceProvider) => new TestingFramework(serviceProvider));
builder.CommandLine.AddProvider(() => new CommandLineOptions());
builder.AddTrxReportProvider();

var application = await builder.BuildAsync();

return await application.RunAsync();