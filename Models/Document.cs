namespace ApiTester.Models;

public record Defaults(
    string? Url,
    Authentication? Authentication,
    Dictionary<string, string>? Headers,
    Dictionary<string, string>? Query,
    string? ContentType
);

public record Request(
    string Method,
    string? Url,
    Authentication? Authentication,
    Dictionary<string, string>? Headers,
    Dictionary<string, string>? Query,
    string? ContentType,
    string? Body);


public record Document(Defaults? Defaults, List<Request> Requests);

