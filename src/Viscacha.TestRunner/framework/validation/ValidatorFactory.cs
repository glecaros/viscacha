using System;
using Viscacha.Model.Test;

namespace Viscacha.TestRunner.Framework.Validation;

internal class ValidatorFactory
{
    public static IValidator Create(ValidationDefinition validation)
    {
        return validation switch
        {
            StatusValidation statusValidation => new StatusValidator(statusValidation),
            PathComparisonValidation pathValidation => new PathComparisonValidator(pathValidation),
            FieldFormatValidation formatValidation => new FieldFormatValidator(formatValidation),
            _ => throw new InvalidOperationException($"Unsupported validation type: {validation.GetType().Name}")
        };
    }
}