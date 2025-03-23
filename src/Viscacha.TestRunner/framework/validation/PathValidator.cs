using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.Model;
using Viscacha.Model.Test;

namespace Viscacha.TestRunner.Framework.Validation;

internal class PathValidator(PathValidation validation) : IValidator
{
    private readonly PathValidation _validation = validation;

    public Task<Result<Error>> ValidateAsync(List<TestVariantResult> testResults, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}