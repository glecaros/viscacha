// See https://aka.ms/new-console-template for more information
using System.Web;
using Azure.Core;
using Azure.Identity;
using YAYL;
using YAYL.Attributes;

Console.WriteLine("Hello, World!");

if (args.Length < 1)
{
    Console.WriteLine("Usage: dotnet run <path-to-yaml>");
    return;
}

var parser = new YamlParser();
if (parser.Parse<HttpRequestInfo>(args[0]) is not HttpRequestInfo request)
{
    Console.WriteLine("Failed to parse YAML");
    return;
}

using var client = new HttpClient();

var builder = new UriBuilder(request.Url);
if (request.Query?.Count > 0)
{
    var query = HttpUtility.ParseQueryString(builder.Query);
    foreach (var (key, value) in request.Query)
    {
        query[key] = value;
    }
    builder.Query = query.ToString();
}

var httpMethod = new HttpMethod(request.Method.ToUpperInvariant());
var httpRequest = new HttpRequestMessage(httpMethod, builder.Uri);

if (request.Headers?.Count > 0)
{
    foreach (var (key, value) in request.Headers)
    {
        httpRequest.Headers.Add(key, value);
    }
}

if (!string.IsNullOrEmpty(request.Body) && httpMethod.AllowsRequestBody())
{
    httpRequest.Content = new StringContent(request.Body);
    if (!string.IsNullOrEmpty(request.ContentType))
    {
        httpRequest.Headers.Add("Content-Type", request.ContentType);
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
}
catch (Exception ex)
{
    Console.WriteLine($"HTTP request failed: {ex.Message}");
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
    Authentication Authentication,
    Dictionary<string, string> Headers,
    Dictionary<string, string> Query,
    string ContentType,
    string Body,
    bool Stream
);

public static class HttpExtensions
{
    public static bool AllowsRequestBody(this HttpMethod method)
    {
        return method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch;
    }
}