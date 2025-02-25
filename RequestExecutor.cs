using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using ApiTester.Models;
using Azure.Core;
using Azure.Identity;

namespace ApiTester;

public class RequestExecutor: IDisposable
{
    private readonly HttpClient _client = new();
    private readonly Dictionary<string, JsonElement> _responses = new();
    private readonly Defaults? _defaults;

    public RequestExecutor(Defaults? defaults)
    {
        _defaults = defaults;
    }

    private Uri ApplyQuery(string url, Dictionary<string, string>? query)
    {
        UriBuilder uriBuilder = new(url);
        if (_defaults?.Query?.Count > 0 || query?.Count > 0)
        {
            var q = HttpUtility.ParseQueryString(uriBuilder.Query);
            if (_defaults?.Query is not null)
            {
                foreach (var (key, value) in _defaults.Query)
                {
                    q[key] = value;
                }
            }
            if (query is not null)
            {
                foreach (var (key, value) in query)
                {
                    q[key] = value;
                }
            }
            uriBuilder.Query = q.ToString();
        }
        return uriBuilder.Uri;
    }

    private static void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string>? headers)
    {
        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                request.Headers.Add(key, value);
            }
        }
    }

    private string ResolveVariables(string input)
    {
        return Constants.ResponseVariableRegex.Replace(input, match =>
        {
            var variable = match.Groups[1].Value;
            if (variable.StartsWith("r"))
            {
                if (variable.Split(".", 2) is [var key, var path] && _responses.TryGetValue(key, out var json))
                {
                    try
                    {
                        JsonElement element = json;
                        foreach (var prop in path.Split('.'))
                        {
                            if (element.TryGetProperty(prop, out var child))
                            {
                                element = child;
                            }
                            else
                            {
                                return match.Value;
                            }
                        }
                        return element.ToString();
                    }
                    catch
                    {
                        return match.Value;
                    }
                }
            }
            return match.Value;
        });
    }


    public string Execute(Models.Request request, int requestIndex)
    {
        var url = request.Url ?? _defaults?.Url;
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("URL is required");
        }
        var method = new HttpMethod(request.Method.ToUpperInvariant());
        var uri = ApplyQuery(url, request.Query);

        using var httpRequest = new HttpRequestMessage(method, uri);
        ApplyHeaders(httpRequest, _defaults?.Headers);
        ApplyHeaders(httpRequest, request.Headers);
        if (!string.IsNullOrEmpty(request.Body) && method.AllowsRequestBody())
        {
            httpRequest.Content = new StringContent(ResolveVariables(request.Body));
            var contentType = request.ContentType ?? _defaults?.ContentType;
            if (!string.IsNullOrEmpty(contentType))
            {
                httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
        }
        var authentication = request.Authentication ?? _defaults?.Authentication;
        if (authentication is ApiKeyAuthentication apiKey)
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
        else if (authentication is AzureCredentialsAuthentication azure)
        {
            var credentials = new DefaultAzureCredential();
            var token = credentials.GetToken(new TokenRequestContext(azure.Scopes));
            httpRequest.Headers.Add("Authorization", $"Bearer {token.Token}");
        }

        using var response = _client.Send(httpRequest);
        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"HTTP request failed: {response.StatusCode}");
            throw new HttpRequestException($"HTTP request failed: {response.StatusCode}");
        }

        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            _responses[$"r{requestIndex}"] = JsonDocument.Parse(content).RootElement;
        }
        return content;
    }




    public void Dispose()
    {
        _client.Dispose();
    }
}