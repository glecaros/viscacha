using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.Model;
using Viscacha.CLI.Test.Model;
using Viscacha.CLI.Test.Util;

namespace Viscacha.CLI.Test.Framework.Validation;

internal sealed class StatusValidator(StatusValidation validation) : IValidator
{
    private readonly StatusValidation _validation = validation;

    private Result<Error> Validate(List<TestVariantResult> testResults)
    {
        Dictionary<string, FrameworkTestVariant> variants = testResults.ToDictionary(r => r.Variant.Name, r => r.Variant);

        var groups = ResponseGrouper.GroupResponsesByRequestIndex(testResults.ToArray());
        switch (_validation.GetEffectiveTarget())
        {
            case Target.All:
                foreach (var group in groups)
                {
                    foreach (var entry in group.Entries)
                    {
                        if (entry.Response.Code != _validation.Status)
                        {
                            return new Error($"Status {entry.Response.Code} does not match expected status {_validation.Status} for variant {entry.Variant}");
                        }
                    }
                }
                break;
            case Target.Single { Index: var index }:
                if (index < 0 || index >= groups.Count)
                {
                    return new Error($"Index {index} out of range.");
                }
                foreach (var entry in groups[index].Entries)
                {
                    if (entry.Response.Code != _validation.Status)
                    {
                        return new Error($"Status {entry.Response.Code} does not match expected status {_validation.Status} for variant {entry.Variant}");
                    }
                }
                break;
            case Target.Multiple { Indices: var indices }:
                foreach (var index in indices)
                {
                    if (index < 0 || index >= groups.Count)
                    {
                        return new Error($"Index {index} out of range.");
                    }
                    foreach (var entry in groups[index].Entries)
                    {
                        if (entry.Response.Code != _validation.Status)
                        {
                            return new Error($"Status {entry.Response.Code} does not match expected status {_validation.Status} for variant {entry.Variant}");
                        }
                    }
                }
                break;
        }
        return new Result<Error>.Ok();
    }

    public Task<Result<Error>> ValidateAsync(List<TestVariantResult> testResults, CancellationToken _) => Task.FromResult(Validate(testResults));
}
