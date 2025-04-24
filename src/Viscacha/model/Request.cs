using System.Collections.Generic;

namespace Viscacha.Model;

public record Request(
    string Method,
    string? Url,
    string? Path,
    Authentication? Authentication,
    Dictionary<string, string>? Headers,
    Dictionary<string, string>? Query,
    string? ContentType,
    string? Body);
