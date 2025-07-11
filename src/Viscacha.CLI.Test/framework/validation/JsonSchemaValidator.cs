using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Json.Pointer;
using Json.Schema;
using Viscacha.Model;
using Viscacha.CLI.Test.Model;
using Viscacha.CLI.Test.Util;

namespace Viscacha.CLI.Test.Framework.Validation;

internal class JsonSchemaValidator(JsonSchemaValidation validation) : IValidator
{
    internal record Schemas(
        JsonSchema Schema,
        List<JsonSchema> Dependencies
    );

    private readonly JsonSchemaValidation _validation = validation;

    private static string WriteReport(EvaluationResults results)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Validation failed for response, details:");

        foreach (var detail in results.Details.Where(d => !d.IsValid))
        {
            sb.AppendLine($"- Schema:   {detail.SchemaLocation}");
            sb.AppendLine($"  Path:     {detail.EvaluationPath}");
            sb.AppendLine($"  Location: {detail.InstanceLocation}");
            if (detail.HasErrors)
            {
                sb.AppendLine($"  Errors:");
                foreach (var (type, error) in detail.Errors!)
                {
                    sb.AppendLine($"  - ({type}): {error}");
                }
            }
        }
        return sb.ToString();
    }

    private static Result<Error> Validate(Schemas schemas, ResponseWrapper response)
    {
        if (response.Content is not { } content)
        {
            return new Error($"Response content is null.");
        }
        if (response.ContentType != "application/json")
        {
            return new Error($"Response content type is not JSON: {response.ContentType}");
        }
        return content.ObjectToJsonNode().Then(node =>
        {
            EvaluationOptions options = new()
            {
                OutputFormat = OutputFormat.List,
            };
            foreach (var dependency in schemas.Dependencies)
            {
                options.SchemaRegistry.Register(dependency);
            }
            var result = schemas.Schema.Evaluate(node, options);
            if (result.IsValid)
            {
                return new Result<Error>.Ok();
            }
            return new Error(WriteReport(result));
        });
    }

    private static Result<Schemas, Error> LoadSchemas(JsonSchemaConfig schema)
    {
        try
        {
            switch (schema)
            {
                case SelfContainedJsonSchema { Path: var path }:
                    var jsonSchema = JsonSchema.FromFile(path);
                    return new Schemas(jsonSchema, []);
                case BundleJsonSchema { Path: var path, RootSelector: var rootSelector }:
                    var pointer = JsonPointer.Parse(rootSelector);
                    var bundle = JsonSchema.FromFile(path);
                    if (bundle is not IBaseDocument)
                    {
                        return new Error($"Bundle schema is not a valid document: {path}");
                    }
                    var rootSchema = (bundle as IBaseDocument).FindSubschema(pointer, EvaluationOptions.Default);
                    if (rootSchema is null)
                    {
                        return new Error($"Root schema not found at {rootSelector} in bundle {path}");
                    }
                    return new Schemas(rootSchema, [bundle]);
                case MultiFileJsonSchema { Path: var path, Dependencies: var dependencies }:
                    var dependencySchemas = dependencies
                        .Select(JsonSchema.FromFile)
                        .ToList();
                    return new Schemas(JsonSchema.FromFile(path), dependencySchemas);
                default:
                    return new Error($"Unsupported schema type: {schema.GetType()}");
            }
        }
        catch (Exception ex)
        {
            return new Error($"Failed to load JSON schema: {ex.Message}");
        }
    }

    private Result<Error> Validate(Schemas schemas, List<ResponseGroup> groups)
    {
        switch (_validation.GetEffectiveTarget())
        {
            case Target.All:
                {
                    foreach (var (index, group) in groups.Enumerate())
                    {
                        foreach (var (variant, response) in group.Entries)
                        {
                            var result = Validate(schemas, response);
                            if (result is Result<Error>.Err { Error: var error })
                            {
                                return new Error($"Validation failed for variant {variant} request with index {index}: {error.Message}");
                            }
                        }
                    }
                    break;
                }
            case Target.Single { Index: var index }:
                {
                    if (index < 0 || index >= groups.Count)
                    {
                        return new Error($"Index {index} out of range.");
                    }
                    foreach (var (variant, response) in groups[index].Entries)
                    {
                        var result = Validate(schemas, response);
                        if (result is Result<Error>.Err { Error: var error })
                        {
                            return new Error($"Validation failed for variant {variant} request with index {index}: {error.Message}");
                        }
                    }
                    break;
                }
            case Target.Multiple { Indices: var indices }:
                {
                    foreach (var index in indices)
                    {
                        if (index < 0 || index >= groups.Count)
                        {
                            return new Error($"Index {index} out of range.");
                        }
                        foreach (var (variant, response) in groups[index].Entries)
                        {
                            var result = Validate(schemas, response);
                            if (result is Result<Error>.Err { Error: var error })
                            {
                                return new Error($"Validation failed for variant {variant} request with index {index}: {error.Message}");
                            }
                        }
                    }
                    break;
                }
        }
        return new Result<Error>.Ok();
    }

    public Result<Error> Validate(List<TestVariantResult> testResults)
    {
        var groups = ResponseGrouper.GroupResponsesByRequestIndex(testResults.ToArray());
        return LoadSchemas(_validation.Schema)
            .Then(schemas =>
            {
                return Validate(schemas, groups);
            });
    }

    public Task<Result<Error>> ValidateAsync(List<TestVariantResult> testResults, CancellationToken _)
    {
        var result = Validate(testResults);
        return Task.FromResult(result);
    }
}