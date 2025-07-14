using System.Collections.Generic;
using YAYL;
using YAYL.Attributes;

namespace Viscacha.CLI.Test.Model;

[YamlPolymorphic("type")]
[YamlDerivedType("status", typeof(StatusValidation))]
[YamlDerivedType("path-comparison", typeof(PathComparisonValidation))]
[YamlDerivedType("field-format", typeof(FieldFormatValidation))]
[YamlDerivedType("json-schema", typeof(JsonSchemaValidation))]
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

[YamlPolymorphic("type")]
[YamlDerivedType("self-contained", typeof(SelfContainedJsonSchema))]
[YamlDerivedType("bundle", typeof(BundleJsonSchema))]
[YamlDerivedType("multi-file", typeof(MultiFileJsonSchema))]
public abstract record JsonSchemaConfig;

public record SelfContainedJsonSchema(
    [property: YamlPathField(YamlFilePathType.RelativeToFile)] string Path
) : JsonSchemaConfig;

public record BundleJsonSchema(
    [property: YamlPathField(YamlFilePathType.RelativeToFile)] string Path,
    string RootSelector
) : JsonSchemaConfig;

public record MultiFileJsonSchema(
    [property: YamlPathField(YamlFilePathType.RelativeToFile)] string Path,
    [property: YamlPathField(YamlFilePathType.RelativeToFile)] List<string> Dependencies
) : JsonSchemaConfig;

public record JsonSchemaValidation(JsonSchemaConfig Schema) : ValidationDefinition;


public static class ValidationExtensions
{
    public static Target GetEffectiveTarget(this ValidationDefinition validation)
    {
        return validation.Target ?? new Target.All();
    }
}
