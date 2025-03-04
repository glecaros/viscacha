namespace ApiTester.Models;

public record Request(
    string Method,
    string? Url,
    string? Path,
    Authentication? Authentication,
    Dictionary<string, string>? Headers,
    Dictionary<string, string>? Query,
    string? ContentType,
    string? Body);
