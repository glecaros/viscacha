using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.Model;
using Viscacha.CLI.Test.Model;
using Viscacha.CLI.Test.Util;

namespace Viscacha.CLI.Test.Framework.Validation;

internal class PathComparisonValidator(PathComparisonValidation validation) : IValidator
{
    private readonly PathComparisonValidation _validation = validation;

    private Result<Error> Validate(Dictionary<string, FrameworkTestVariant> variants, ResponseGroup responseGroup, int index)
    {
        List<(FrameworkTestVariant variant, HashSet<string> paths)> variantPaths = [];
        foreach (var (variantName, response) in responseGroup.Entries)
        {
            if (!variants.TryGetValue(variantName, out var variant))
            {
                return new Error($"Variant {variantName} not found.");
            }

            if (response.ContentType != "application/json")
            {
                return new Error($"Response content type is not JSON for variant {variantName}: {response.ContentType}");
            }

            PathExtractor extractor = new(response, _validation.PreserveArrayIndices ?? false);
            var paths = extractor.ExtractPaths();
            variantPaths.Add((variant, paths));
        }
        var baselinePaths = variantPaths.FirstOrDefault(v => v.variant.Name == _validation.Baseline);
        if (baselinePaths == default)
        {
            return new Error("No baseline variant found.");
        }
        StringBuilder sb = new();
        foreach (var (variant, paths) in variantPaths.Where(v => v.variant.Name != _validation.Baseline))
        {
            HashSet<string> difference = [.. paths];
            difference.SymmetricExceptWith(baselinePaths.paths);
            difference.ExceptWith(paths);
            if (_validation.IgnorePaths is not null)
            {
                difference.ExceptWith(_validation.IgnorePaths);
            }

            if (difference.Count > 0)
            {
                sb.AppendLine($"Variant {variant.Name} is missing paths: {string.Join(", ", difference)}");
            }
        }
        if (sb.Length > 0)
        {
            return new Error($"Validation failed for requests with index {index}:\n{sb}");
        }

        return new Result<Error>.Ok();
    }

    private Result<Error> Validate(List<TestVariantResult> testResults)
    {
        Dictionary<string, FrameworkTestVariant> variants = testResults.ToDictionary(r => r.Variant.Name, r => r.Variant);

        var groups = ResponseGrouper.GroupResponsesByRequestIndex(testResults.ToArray());
        switch (_validation.GetEffectiveTarget())
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

    public Task<Result<Error>> ValidateAsync(List<TestVariantResult> testResults, CancellationToken _)
    {
        var result = Validate(testResults);
        return Task.FromResult(result);
    }
}