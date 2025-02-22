using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web;
using Azure.Core;
using Azure.Identity;
using dotenv.net;
using YAYL;
using YAYL.Attributes;
using System.Text.Json;

DotEnv.Load();

if (args.Length < 1)
{
    Console.WriteLine("Usage: dotnet run <path-to-yaml>");
    return;
}

var parser = new YamlParser();
parser.AddVariableResolver(EnvironmentVariableRegex, variable => Environment.GetEnvironmentVariable(variable) ?? "");

// Replace the current YAML parsing block with logic to accept a collection or a single request.
List<HttpRequestInfo> requests;
if (parser.ParseFile<Document>(args[0]) is Document doc && doc.Requests is not null)
{
    requests = doc.Requests;
}
else if (parser.ParseFile<HttpRequestInfo>(args[0]) is HttpRequestInfo single)
{
    requests = new List<HttpRequestInfo> { single };
}
else
{
    Console.WriteLine("Failed to parse YAML");
    return;
}

// Custom resolver to substitute variables from previous responses.
string ResolveVariables(string input, Dictionary<string, JsonElement> responses)
{
    return Regex.Replace(input, @"#\{([^}]+)\}", match =>
    {
        var variable = match.Groups[1].Value;
        // Expected format: rN.path.to.field (for JSON responses)
        if (variable.StartsWith("r"))
        {
            var parts = variable.Split('.', 2);
            if (parts.Length == 2 && responses.TryGetValue(parts[0], out var json))
            {
                try
                {
                    // Use simple JSON pointer by navigating property names separated by dot.
                    using var doc = JsonDocument.Parse(json.GetRawText());
                    JsonElement element = doc.RootElement;
                    foreach (var prop in parts[1].Split('.'))
                    {
                        if (element.TryGetProperty(prop, out var child))
                        {
                            element = child;
                        }
                        else
                        {
                            return match.Value; // Return the original match if not found.
                        }
                    }
                    return element.ToString();
                }
                catch
                {
                    return match.Value; // Return the original match if any error occurs.
                }
            }
        }
        return match.Value; // Return the original match if not found.
    });
}

using var client = new HttpClient();
var responses = new Dictionary<string, JsonElement>();

foreach (var request in requests)
{
    var builder = new UriBuilder(ResolveVariables(request.Url, responses));
    if (request.Query?.Count > 0)
    {
        var query = HttpUtility.ParseQueryString(builder.Query);
        foreach (var (key, value) in request.Query)
        {
            query[ResolveVariables(key, responses)] = ResolveVariables(value, responses);
        }
        builder.Query = query.ToString();
    }

    var httpMethod = new HttpMethod(request.Method.ToUpperInvariant());
    var httpRequest = new HttpRequestMessage(httpMethod, builder.Uri);

    if (request.Headers?.Count > 0)
    {
        foreach (var (key, value) in request.Headers)
        {
            httpRequest.Headers.Add(ResolveVariables(key, responses), ResolveVariables(value, responses));
        }
    }

    if (!string.IsNullOrEmpty(request.Body) && httpMethod.AllowsRequestBody())
    {
        httpRequest.Content = new StringContent(ResolveVariables(request.Body, responses));
        if (!string.IsNullOrEmpty(request.ContentType))
        {
            httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ResolveVariables(request.ContentType, responses));
        }
    }

    if (request.Authentication is ApiKeyAuthentication apiKey)
    {
        var headerName = apiKey.Header switch
        {
            var v when string.IsNullOrEmpty(v) => "X-Api-Key",
            _ => apiKey.Header!
        };
        var headerValue = apiKey.Prefix switch
        {
            var v when string.IsNullOrWhiteSpace(v) => apiKey.Key,
            _ => $"{apiKey.Prefix} {apiKey.Key}"
        };
        httpRequest.Headers.Add(headerName, headerValue);
    }
    else if (request.Authentication is AzureCredentialsAuthentication azure)
    {
        var credentials = new DefaultAzureCredential();
        var token = await credentials.GetTokenAsync(new TokenRequestContext(azure.Scopes));
        httpRequest.Headers.Add("Authorization", $"Bearer {token.Token}");
    }

    try
    {
        var response = await client.SendAsync(httpRequest);
        response.Content.ReadAsStreamAsync().Result.CopyTo(Console.OpenStandardOutput());

        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var jsonResponse = await response.Content.ReadAsStringAsync();
            responses[$"r{requests.IndexOf(request)}"] = JsonDocument.Parse(jsonResponse).RootElement;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HTTP request failed: {ex.Message}");
    }
}

[YamlPolymorphic("type")]
[YamlDerivedType("api-key", typeof(ApiKeyAuthentication))]
[YamlDerivedType("azure-credentials", typeof(AzureCredentialsAuthentication))]
public record Authentication();

public record AzureCredentialsAuthentication(string[] Scopes) : Authentication;

public record ApiKeyAuthentication(string Key) : Authentication
{
    public string? Header { get; init; }
    public string? Prefix { get; init; }
};

public record HttpRequestInfo(
    string Method,
    string Url,
    Authentication? Authentication,
    Dictionary<string, string>? Headers,
    Dictionary<string, string>? Query,
    string? ContentType,
    string? Body
);

public record Document(List<HttpRequestInfo> Requests);

public static class HttpExtensions
{
    public static bool AllowsRequestBody(this HttpMethod method)
    {
        return method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch;
    }
}
partial class Program
{
    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex EnvironmentVariableRegex { get; }
}