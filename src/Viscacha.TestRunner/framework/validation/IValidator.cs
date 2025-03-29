using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Viscacha.Model;

namespace Viscacha.TestRunner.Framework.Validation;

internal interface IValidator
{
    Task<Result<Error>> ValidateAsync(List<TestVariantResult> testResults, CancellationToken cancellationToken);
}
