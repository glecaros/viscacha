using dotenv.net;
using System.CommandLine;
using Viscacha.CLI.Request;
using Viscacha.CLI.Test;

DotEnv.Load();

var rootCommand = new RootCommand("Viscacha - Tool for executing and testing HTTP endpoints.")
{
    RequestCommand.Create(),
    TestCommand.Create(),
};

return rootCommand.Parse(args).Invoke();
