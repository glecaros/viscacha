using System.CommandLine;
using dotenv.net;
using Viscacha.CLI.Request;
using Viscacha.CLI.Test;

DotEnv.Load();

var rootCommand = new RootCommand("Viscacha - Tool for executing and testing HTTP endpoints.")
{
    RequestCommand.Create(),
    TestCommand.Create(),
};

return rootCommand.Parse(args).Invoke();
