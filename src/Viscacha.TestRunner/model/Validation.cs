using System.Collections.Generic;
using YAYL.Attributes;

namespace Viscacha.Model.Test;

[YamlPolymorphic("type")]
[YamlDerivedType("status", typeof(StatusValidation))]
[YamlDerivedType("path-comparison", typeof(PathComparisonValidation))]
[YamlDerivedType("field-format", typeof(FieldFormatValidation))]
public abstract record ValidationDefinition
{
    public Target? Target { get; init; }
}


public record StatusValidation(int Status) : ValidationDefinition;

public record PathComparisonValidation(string Baseline) : ValidationDefinition
{
    public HashSet<string>? IgnorePaths { get; init; }
    public bool? PreserveArrayIndices { get; init; }
}

public enum Format
{
    Json,
}

public record FieldFormatValidation(string Path, Format Format) : ValidationDefinition;


public static class ValidationExtensions
{
    public static Target GetEffectiveTarget(this ValidationDefinition validation)
    {
        return validation.Target ?? new Target.All();
    }
}
