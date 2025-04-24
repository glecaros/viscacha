using System.Collections.Generic;
using YAYL.Attributes;

namespace Viscacha.Model.Test;

[YamlPolymorphic("type")]
[YamlDerivedType("status", typeof(StatusValidation))]
[YamlDerivedType("path-comparison", typeof(PathComparisonValidation))]
[YamlDerivedType("field-format", typeof(FieldFormatValidation))]
public abstract record ValidationDefinition
{
    public Target Target { get; init; } = new Target.All();
}

public record StatusValidation(int Status) : ValidationDefinition;

public record PathComparisonValidation(string Baseline) : ValidationDefinition
{
    public HashSet<string> IgnorePaths { get; init; } = new();
}

public enum Format
{
    Json,
}

public record FieldFormatValidation(string Path, Format Format) : ValidationDefinition;
