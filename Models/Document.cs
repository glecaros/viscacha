namespace ApiTester.Models;

public record RequestConfig(
    string? Url,
    Authentication? Authentication,
    Dictionary<string, string>? Headers,
    Dictionary<string, string>? Query,
    string? ContentType
);

public record Document(RequestConfig? BaseConfig, List<HttpRequestInfo> Requests);

public record HttpRequestInfo(
    string Method,
    string? Url,
    Authentication? Authentication,
    Dictionary<string, string>? Headers,
    Dictionary<string, string>? Query,
    string? ContentType,
    string? Body)
{
    public HttpRequestInfo GetEffectiveRequest(RequestConfig? baseConfig) =>
        new(
            Method,
            Url ?? baseConfig?.Url,
            Authentication ?? baseConfig?.Authentication,
            Headers ?? baseConfig?.Headers,
            Query ?? baseConfig?.Query,
            ContentType ?? baseConfig?.ContentType,
            Body
        );
}
