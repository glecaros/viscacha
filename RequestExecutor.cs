using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using ApiTester.Models;
using Azure.Core;
using Azure.Identity;

namespace ApiTester;

internal static class HttpExtensions
{
    public static bool AllowsRequestBody(this HttpMethod method)
    {
        return method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch;
    }
}

public record ResponseWrapper(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("content")] object? Content);

internal record SSEEvent(
    [property: JsonPropertyName("event")] string EventName,
    [property: JsonPropertyName("data")] string Data);

public class RequestExecutor: IDisposable
{
    private readonly HttpClient _client = new();
    private readonly Dictionary<string, JsonElement> _responses = new();
    private readonly Dictionary<string, string> _commandLineVariables = new();
    private readonly Defaults? _defaults;

    public RequestExecutor(Defaults? defaults, Dictionary<string, string>? commandLineVariables = null)
    {
        _defaults = defaults;
        if (commandLineVariables != null)
        {
            _commandLineVariables = commandLineVariables;
        }
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
        var result = Constants.ResponseVariableRegex.Replace(input, match =>
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

        return Constants.CommandLineVariableRegex.Replace(result, match =>
        {
            var variable = match.Groups[1].Value;
            return _commandLineVariables.TryGetValue(variable, out var value) ? value : match.Value;
        });
    }

    private string GetUrl(Models.Request request)
    {
        if (request.Url is not null)
        {
            return request.Url;
        }
        if (_defaults?.BaseUrl is not null)
        {
            if (request.Path is not null)
            {
                return $"{_defaults.BaseUrl}{request.Path}";
            }
            return _defaults.BaseUrl;
        }
        throw new ArgumentException("URL is required");
    }

    private async Task<object?> HandleApplicationJson(HttpContent content, int requestIndex)
    {
        var stringContent = await content.ReadAsStringAsync().ConfigureAwait(false);
        _responses[$"r{requestIndex}"] = JsonDocument.Parse(stringContent).RootElement;
        return JsonSerializer.Deserialize<object>(stringContent);
    }

    private async Task<object?> HandleSSE(HttpContent content)
    {
        using var stream = await content.ReadAsStreamAsync();
        var parser = SseParser.Create(stream);
        List<SSEEvent> sseItems = [];
        await foreach (SseItem<string> item in parser.EnumerateAsync())
        {
            sseItems.Add(new SSEEvent(item.EventType, item.Data));
        }
        return sseItems;
    }

    public ResponseWrapper Execute(Models.Request request, int requestIndex)
    {
        var url = ResolveVariables(GetUrl(request));
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

        var responseContentType = response.Content.Headers.ContentType?.MediaType;
        object? content = responseContentType switch
        {
            "application/json" => HandleApplicationJson(response.Content, requestIndex).GetAwaiter().GetResult(),
            "text/event-stream" => HandleSSE(response.Content).GetAwaiter().GetResult(),
            _ => null
        };
        return new((int)response.StatusCode, content);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}