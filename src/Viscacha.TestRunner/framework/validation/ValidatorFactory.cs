using System;
using Viscacha.TestRunner.Model;

namespace Viscacha.TestRunner.Framework.Validation;

internal class ValidatorFactory
{
    public static IValidator Create(ValidationDefinition validation) => validation switch
    {
        StatusValidation statusValidation => new StatusValidator(statusValidation),
        PathComparisonValidation pathValidation => new PathComparisonValidator(pathValidation),
        FieldFormatValidation formatValidation => new FieldFormatValidator(formatValidation),
        JsonSchemaValidation jsonSchemaValidation => new JsonSchemaValidator(jsonSchemaValidation),
        _ => throw new InvalidOperationException($"Unsupported validation type: {validation.GetType().Name}")
    };
}