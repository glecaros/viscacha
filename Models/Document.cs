namespace ApiTester.Models;

public record Defaults(
    string? Import,
    string? BaseUrl,
    Authentication? Authentication,
    Dictionary<string, string>? Headers,
    Dictionary<string, string>? Query,
    string? ContentType
)
{
    public static Defaults Empty => new(null, null, null, null, null, null);
};

public record Document(Defaults? Defaults, List<Request> Requests);
