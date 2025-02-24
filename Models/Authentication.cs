using YAYL.Attributes;

namespace ApiTester.Models;

[YamlPolymorphic("type")]
[YamlDerivedType("api-key", typeof(ApiKeyAuthentication))]
[YamlDerivedType("azure-credentials", typeof(AzureCredentialsAuthentication))]
public record Authentication();

public record AzureCredentialsAuthentication(string[] Scopes) : Authentication;

public record ApiKeyAuthentication(string Key) : Authentication
{
    public string? Header { get; init; }
    public string? Prefix { get; init; }
}
