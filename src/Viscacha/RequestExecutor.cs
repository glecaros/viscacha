using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Azure.Core;
using Azure.Identity;
using Viscacha.Model;

namespace Viscacha;

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

public class RequestExecutor(Defaults? defaults = null)
{
    private readonly Dictionary<string, JsonElement> _responses = new();
    private readonly Defaults? _defaults = defaults;

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

    private Result<string, Error> GetUrl(Model.Request request)
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
        return new Error("URL is required");
    }

    private async Task<object?> HandleApplicationJsonAsync(HttpContent content, int requestIndex, CancellationToken cancellationToken)
    {
        var stringContent = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _responses[$"r{requestIndex}"] = JsonDocument.Parse(stringContent).RootElement;
        return JsonSerializer.Deserialize<object>(stringContent);
    }

    private async Task<object?> HandleSSEAsync(HttpContent content, CancellationToken cancellationToken)
    {
        using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var parser = SseParser.Create(stream);
        List<SSEEvent> sseItems = [];
        await foreach (SseItem<string> item in parser.EnumerateAsync(cancellationToken))
        {
            sseItems.Add(new SSEEvent(item.EventType, item.Data));
        }
        return sseItems;
    }

    public Result<ResponseWrapper, Error> Execute(Model.Request request, int requestIndex)
    {
        return ExecuteAsync(request, requestIndex).GetAwaiter().GetResult();
    }

    public async Task<Result<ResponseWrapper, Error>> ExecuteAsync(Model.Request request, int requestIndex)
    {
        using HttpClient client = new();
        return await ExecuteAsync(client, request, requestIndex).ConfigureAwait(false);
    }

    public Result<ResponseWrapper, Error> Execute(HttpClient client, Model.Request request, int requestIndex)
    {
        return ExecuteAsync(client, request, requestIndex).GetAwaiter().GetResult();
    }

    public Task<Result<ResponseWrapper, Error>> ExecuteAsync(HttpClient client, Model.Request request, int requestIndex, CancellationToken cancellationToken = default)
    {
        return GetUrl(request).Map(ResolveVariables).Then<ResponseWrapper>(async url =>
        {
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
                var token = await credentials.GetTokenAsync(new TokenRequestContext(azure.Scopes), cancellationToken).ConfigureAwait(false);
                httpRequest.Headers.Add("Authorization", $"Bearer {token.Token}");
            }

            using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

            var responseContentType = response.Content.Headers.ContentType?.MediaType;
            object? content = responseContentType switch
            {
                "application/json" => await HandleApplicationJsonAsync(response.Content, requestIndex, cancellationToken),
                "text/event-stream" => await HandleSSEAsync(response.Content, cancellationToken),
                _ => null
            };
            return new ResponseWrapper((int)response.StatusCode, content);
        });
    }
}