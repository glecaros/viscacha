using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.Model;
using Viscacha.Model.Test;
using Viscacha.TestRunner.Util;

namespace Viscacha.TestRunner.Framework.Validation;

internal class FieldFormatValidator(FieldFormatValidation validation) : IValidator
{
    private readonly FieldFormatValidation _validation = validation;
    private readonly FieldExtractor _extractor = new(validation.Path);

    private Result<Error> ValidateJson(Dictionary<string, FrameworkTestVariant> variants, ResponseGroup responseGroup, int index)
    {
        foreach (var (variantName, response) in responseGroup.Entries)
        {
            if (!variants.TryGetValue(variantName, out var variant))
            {
                return new Error($"Variant {variantName} not found.");
            }

            if (response.Content is null)
            {
                return new Error($"Response content is null for variant {variantName}.");
            }

            var extractionResult = _extractor.ExtractFields<string>(response.Content);

            if (!extractionResult.IsSuccess)
            {
                return extractionResult.UnwrapError();
            }

            var fieldValues = extractionResult.Unwrap();

            foreach (var fieldValue in fieldValues)
            {
                if (fieldValue is null)
                {
                    return new Error($"Field value is null for variant {variantName}.");
                }

                try
                {
                    if (JsonSerializer.Deserialize<object>(fieldValue) is null)
                    {
                        return new Error($"Failed to deserialize field value for variant {variantName}: null value.");
                    }
                }
                catch (JsonException ex)
                {
                    return new Error($"Failed to deserialize field value for variant {variantName}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    return new Error($"Unexpected error while processing field value for variant {variantName}: {ex.Message}");
                }
            }
        }
        
        return new Result<Error>.Ok();
    }

    private Result<Error> Validate(Dictionary<string, FrameworkTestVariant> variants, ResponseGroup responseGroup, int index) => _validation.Format switch
    {
        Format.Json => ValidateJson(variants, responseGroup, index),
        _ => throw new InvalidOperationException($"Unsupported format: {_validation.Format}"),
    };


    private Result<Error> Validate(List<TestVariantResult> testResults)
    {
        Dictionary<string, FrameworkTestVariant> variants = testResults.ToDictionary(r => r.Variant.Name, r => r.Variant);

        var groups = ResponseGrouper.GroupResponsesByRequestIndex(testResults.ToArray());

        switch (_validation.Target)
        {
            case Target.All:
            {
                int index = 0;
                foreach (var group in groups)
                {
                    if (Validate(variants, group, index++) is Result<Error>.Err error)
                    {
                        return error;
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
                return Validate(variants, groups[index], index);
            }
            case Target.Multiple { Indices: var indices }:
            {
                foreach (var index in indices)
                {
                    if (index < 0 || index >= groups.Count)
                    {
                        return new Error($"Index {index} out of range.");
                    }
                    if (Validate(variants, groups[index], index) is Result<Error>.Err error)
                    {
                        return error;
                    }
                }
                break;
            }
        }

        return new Result<Error>.Ok();
    }

    public Task<Result<Error>> ValidateAsync(List<TestVariantResult> testResults, CancellationToken cancellationToken)
    {
        var result = Validate(testResults);
        return Task.FromResult(result);
    }
}