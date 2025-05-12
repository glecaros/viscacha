using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Json.Schema;
using Viscacha.Model;
using Viscacha.TestRunner.Model;
using Viscacha.TestRunner.Util;

namespace Viscacha.TestRunner.Framework.Validation;

internal class JsonSchemaValidator(JsonSchemaValidation validation) : IValidator
{
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

    private static Result<Error> Validate(JsonSchema schema, ResponseWrapper response)
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
            var result = schema.Evaluate(node, new()
            {
                OutputFormat = OutputFormat.List,
            });
            if (result.IsValid)
            {
                return new Result<Error>.Ok();
            }
            return new Error(WriteReport(result));
        });
    }

    private static Result<JsonSchema, Error> LoadSchema(string schemaFile)
    {
        try
        {
            return JsonSchema.FromFile(schemaFile);
        }
        catch (Exception ex)
        {
            return new Error($"Failed to load JSON schema: {ex.Message}");
        }
    }

    private Result<Error> Validate(JsonSchema schema, List<ResponseGroup> groups)
    {
        switch (_validation.GetEffectiveTarget())
        {
            case Target.All:
                {
                    foreach (var (index, group) in groups.Enumerate())
                    {
                        foreach (var (variant, response) in group.Entries)
                        {
                            var result = Validate(schema, response);
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
                        var result = Validate(schema, response);
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
                            var result = Validate(schema, response);
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
        return LoadSchema(_validation.SchemaFile)
            .Then(schema =>
            {
                return Validate(schema, groups);
            });
    }

    public Task<Result<Error>> ValidateAsync(List<TestVariantResult> testResults, CancellationToken _)
    {
        var result = Validate(testResults);
        return Task.FromResult(result);
    }
}