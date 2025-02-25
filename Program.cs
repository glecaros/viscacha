using dotenv.net;
using YAYL;
using ApiTester.Models;
using ApiTester;

DotEnv.Load();

if (args.Length < 1)
{
    Console.WriteLine("Usage: dotnet run <path-to-yaml>");
    return;
}

var parser = new YamlParser();
parser.AddVariableResolver(Constants.EnvironmentVariableRegex, variable => Environment.GetEnvironmentVariable(variable) ?? "");

if (parser.ParseFile<Document>(args[0]) is not Document doc)
{
    Console.WriteLine("Failed to parse YAML");
    return;
}

using var executor = new RequestExecutor(doc.Defaults);

foreach (var request in doc.Requests)
{
    try
    {
        var response = executor.Execute(request, doc.Requests.IndexOf(request));
        Console.WriteLine($"Response: {response}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HTTP request failed: {ex.Message}");
    }
}

public static class HttpExtensions
{
    public static bool AllowsRequestBody(this HttpMethod method)
    {
        return method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch;
    }
}