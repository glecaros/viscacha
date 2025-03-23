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
            PathValidation pathValidation => new PathValidator(pathValidation),
            FormatValidation formatValidation => new FormatValidator(formatValidation),
            SemanticValidation semanticValidation => new SemanticValidator(semanticValidation),
            _ => throw new InvalidOperationException($"Unsupported validation type: {validation.GetType().Name}")
        };
    }
}