using System.Collections.Generic;
using YAYL.Attributes;

namespace Viscacha.Model.Test;

[YamlPolymorphic("type")]
[YamlDerivedType("status", typeof(StatusValidation))]
[YamlDerivedType("path", typeof(PathValidation))]
[YamlDerivedType("format", typeof(FormatValidation))]
[YamlDerivedType("semantic", typeof(SemanticValidation))]
public abstract record Validation
{
    public Target? Target { get; init; }
}

public record StatusValidation(int Status) : Validation;

public record PathValidation(string Baseline) : Validation
{
    public List<string> IgnorePaths { get; init; } = new();
}

public enum Format
{
    Json,
}

public record FormatValidation(Format Format, string Path) : Validation;

public record SemanticValidation(string Provider, string Path, string Expectation) : Validation;
