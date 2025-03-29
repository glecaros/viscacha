using System.Collections.Generic;
using YAYL.Attributes;

namespace Viscacha.Model.Test;

[YamlPolymorphic("type")]
[YamlDerivedType("status", typeof(StatusValidation))]
[YamlDerivedType("path", typeof(PathValidation))]
[YamlDerivedType("format", typeof(FormatValidation))]
[YamlDerivedType("semantic", typeof(SemanticValidation))]
public abstract record ValidationDefinition
{
    public Target? Target { get; init; }
}

public record StatusValidation(int Status) : ValidationDefinition;

public record PathValidation(string Baseline) : ValidationDefinition
{
    public HashSet<string> IgnorePaths { get; init; } = new();
}

public enum Format
{
    Json,
}

public record FormatValidation(Format Format, string Path) : ValidationDefinition;

public record SemanticValidation(string Provider, string Path, string Expectation) : ValidationDefinition;
