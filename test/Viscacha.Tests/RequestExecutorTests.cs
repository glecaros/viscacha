using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.Model;

namespace Viscacha.Tests;

public class RequestExecutorTests
{
    private MockHttpMessageHandler _mockHttp;
    private HttpClient _httpClient;
    private RequestExecutor _executor;
    private Defaults _defaults;

    [SetUp]
    public void Setup()
    {
        _mockHttp = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHttp);

        _defaults = new Defaults(
            Import: null,
            BaseUrl: "https://api.example.com",
            Authentication: null,
            Headers: new Dictionary<string, string> {
                ["Accept"] = "application/json",
            },
            Query: new Dictionary<string, string> {
                ["api-version"] = "1.0",
            },
            ContentType: "application/json"
        );

        _executor = new(_defaults);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
        _mockHttp.Dispose();

    }

    [Test]
    public void Execute_WithUrlOnly_SendsRequest()
    {
        Request request = new("GET", "https://api.test.com/users", null, null, null, null, null, null);
        _mockHttp.SetupResponse(HttpStatusCode.OK, new { id = 1, name = "Test User" });

        var result = _executor.Execute(_httpClient, request, 0);
        Assert.That(result is Result<ResponseWrapper, Error>.Ok);

        var response = result.Unwrap();
        Assert.That(response.Code, Is.EqualTo(200));
        Assert.That(_mockHttp.RequestUri?.ToString(), Is.EqualTo("https://api.test.com/users?api-version=1.0"));
        Assert.That(_mockHttp.Method, Is.EqualTo(HttpMethod.Get));
    }

    [Test]
    public void Execute_WithBaseUrlAndPath_CombinesUrls()
    {
        Request request = new("GET", null, "/users", null, null, null, null, null);
        _mockHttp.SetupResponse(HttpStatusCode.OK, new { id = 1, name = "Test User" });

        var result = _executor.Execute(_httpClient, request, 0);

        Assert.That(result is Result<ResponseWrapper, Error>.Ok);
        Assert.That(_mockHttp.RequestUri?.ToString(), Is.EqualTo("https://api.example.com/users?api-version=1.0"));
    }

    [Test]
    public void Execute_WithQueryParameters_MergesQueryParameters()
    {
        Request request = new(
            "GET",
            "https://api.test.com/users",
            null,
            null,
            null,
            new() {
                ["filter"] = "active",
            },
            null,
            null);
        _mockHttp.SetupResponse(HttpStatusCode.OK);

        var result = _executor.Execute(_httpClient, request, 0);

        Assert.That(result is Result<ResponseWrapper, Error>.Ok);
        Assert.That(_mockHttp.RequestUri?.ToString(), Is.EqualTo("https://api.test.com/users?api-version=1.0&filter=active"));
    }

    [Test]
    public void Execute_WithHeaders_SendsHeaders()
    {
        Request request = new (
            "GET",
            null,
            "/users",
            null,
            new () {
                ["Custom-Header"] = "test-value"
            },
            null,
            null,
            null);
        _mockHttp.SetupResponse(HttpStatusCode.OK);

        var result = _executor.Execute(_httpClient, request, 0);
        Assert.That(result is Result<ResponseWrapper, Error>.Ok);

        Assert.That(_mockHttp?.Headers?.Contains("Custom-Header"), Is.True);
        Assert.That(_mockHttp?.Headers?.GetValues("Custom-Header"), Does.Contain("test-value"));
        Assert.That(_mockHttp?.Headers?.Contains("Accept"), Is.True);
        Assert.That(_mockHttp?.Headers?.GetValues("Accept"), Does.Contain("application/json"));
    }


    [Test]
    public void Execute_WithApiKeyAuthentication_AddsAuthHeader()
    {
        ApiKeyAuthentication auth = new("api-key")
        {
            Header = "X-API-Key",
        };
        Request request = new("GET", null, "/users", auth, null, null, null, null);
        _mockHttp.SetupResponse(HttpStatusCode.OK);

        var result = _executor.Execute(_httpClient, request, 0);
        Assert.That(result is Result<ResponseWrapper, Error>.Ok);

        Assert.That(_mockHttp?.Headers?.Contains("X-API-Key"), Is.True);
        Assert.That(_mockHttp?.Headers?.GetValues("X-API-Key"), Does.Contain("api-key"));
    }

    [Test]
    public void Execute_WithBody_SendsRequestBody()
    {
        Request request = new(
            "POST",
            null,
            "/users",
            null,
            null,
            null,
            null,
            "{\"name\":\"New User\"}");
        _mockHttp.SetupResponse(HttpStatusCode.OK);

        var result = _executor.Execute(_httpClient, request, 0);
        Assert.That(result is Result<ResponseWrapper, Error>.Ok);

        Assert.That(_mockHttp.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(_mockHttp.RequestContent, Is.EqualTo("{\"name\":\"New User\"}"));
        Assert.That(_mockHttp.ContentType, Is.EqualTo("application/json"));
    }

    [Test]
    public void Execute_WithJsonResponse_ParsesJsonResponse()
    {
        Request request = new ("GET", null, "/users/1", null, null, null, null, null);
        _mockHttp.SetupResponse(HttpStatusCode.OK, new { id = 1, name = "Test User" });

        var result = _executor.Execute(_httpClient, request, 0);
        Assert.That(result is Result<ResponseWrapper, Error>.Ok);

        var response = result.Unwrap();
        Assert.That(response.Content, Is.Not.Null);

        var content = response.Content as JsonElement?;
        Assert.That(content?.GetProperty("id").GetInt32(), Is.EqualTo(1));
        Assert.That(content?.GetProperty("name").GetString(), Is.EqualTo("Test User"));
    }

    [Test]
    public void Execute_WithVariableResolutionInBody_ResolvesVariables()
    {
        Request request1 = new ("GET", null, "/users/1", null, null, null, null, null);
        _mockHttp.SetupResponse(HttpStatusCode.OK, new { id = 1, name = "Test User" });
        _executor.Execute(_httpClient, request1, 0);

        Request request2 = new(
            "POST",
            null,
            "/users",
            null,
            null,
            null,
            null,
            "{\"parentId\": #{r0.id}}");
        _mockHttp.SetupResponse(HttpStatusCode.Created);

        var result = _executor.Execute(_httpClient, request2, 1);

        Assert.That(result is Result<ResponseWrapper, Error>.Ok);
        Assert.That(_mockHttp.RequestContent, Is.EqualTo("{\"parentId\": 1}"));
    }


    [Test]
    public void Execute_WithMissingUrl_ReturnsError()
    {
        Defaults defaults = new (null, null, null, null, null, null);
        RequestExecutor executor = new(defaults);
        Request request = new ("GET", null, null, null, null, null, null, null);

        var result = executor.Execute(_httpClient, request, 0);
        Assert.That(result is Result<ResponseWrapper, Error>.Err);

        var error = result.UnwrapError();
        Assert.That(error.Message, Is.EqualTo("URL is required"));
    }

    [Test]
    public void Execute_CapturesResponseHeaders_InResponseWrapper()
    {
        Request request = new("GET", null, "/users", null, null, null, null, null);
        _mockHttp.SetupResponseHeaders(new Dictionary<string, string[]> {
            ["Content-Type"] = ["application/json"],
            ["X-Request-ID"] = ["abc123"],
            ["Set-Cookie"] = ["session=xyz; path=/", "tracking=123; path=/api"]
        });
        _mockHttp.SetupResponse(HttpStatusCode.OK, new { id = 1, name = "Test User" });

        var result = _executor.Execute(_httpClient, request, 0);
        Assert.That(result is Result<ResponseWrapper, Error>.Ok);

        var response = result.Unwrap();
        Assert.That(response.Headers, Is.Not.Null);
        Assert.That(response.Headers.ContainsKey("X-Request-ID"), Is.True);
        Assert.That(response.Headers["X-Request-ID"], Is.EqualTo(new List<string> { "abc123" }));
        Assert.That(response.Headers.ContainsKey("Set-Cookie"), Is.True);
        Assert.That(response.Headers["Set-Cookie"].Count, Is.EqualTo(2));
        Assert.That(response.Headers["Set-Cookie"], Does.Contain("session=xyz; path=/"));
        Assert.That(response.Headers["Set-Cookie"], Does.Contain("tracking=123; path=/api"));
    }

    [Test]
    public void Execute_WithEmptyResponseHeaders_ReturnsEmptyHeadersDictionary()
    {
        Request request = new("GET", null, "/empty-headers", null, null, null, null, null);
        _mockHttp.SetupResponseHeaders(new Dictionary<string, string[]>());
        _mockHttp.SetupResponse(HttpStatusCode.NoContent);

        var result = _executor.Execute(_httpClient, request, 0);
        Assert.That(result is Result<ResponseWrapper, Error>.Ok);

        var response = result.Unwrap();
        Assert.That(response.Headers, Is.Not.Null);
        Assert.That(response.Headers.Count, Is.EqualTo(0));
    }

}

public class MockHttpMessageHandler : HttpMessageHandler
{
    public HttpMethod? Method { get; private set; }
    public Uri? RequestUri { get; private set; }
    public HttpRequestHeaders? Headers { get; private set; }
    public string? RequestContent { get; private set; }
    public string? ContentType { get; private set; }

    private HttpStatusCode _responseStatusCode = HttpStatusCode.OK;
    private object? _responseContent;
    private string _responseContentType = "application/json";
    private Dictionary<string, string[]>? _responseHeaders;

    public void SetupResponseHeaders(Dictionary<string, string[]> headers)
    {
        _responseHeaders = headers;
    }

    public void SetupResponse(HttpStatusCode statusCode, object? content = null, string contentType = "application/json")
    {
        _responseStatusCode = statusCode;
        _responseContent = content;
        _responseContentType = contentType;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Method = request.Method;
        RequestUri = request.RequestUri;
        Headers = request.Headers;

        if (request.Content != null)
        {
            RequestContent = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            ContentType = request.Content.Headers.ContentType?.MediaType;
        }

        var response = new HttpResponseMessage(_responseStatusCode);
        if (_responseHeaders != null)
        {
            foreach (var header in _responseHeaders)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (_responseContent != null)
        {
            string content = _responseContent is string s
                ? s
                : JsonSerializer.Serialize(_responseContent);

            response.Content = new StringContent(content, Encoding.UTF8, _responseContentType);
        }
        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Send(request, cancellationToken));
    }
}
