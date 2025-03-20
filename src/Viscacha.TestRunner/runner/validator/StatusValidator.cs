using System;
using System.Collections;
using System.Collections.Generic;
using Viscacha.Model.Test;
using Viscacha.Runner;

namespace Viscacha.TestRunner.Runner.Validator;

public record ValidationResult(bool Success)
{
    public string? Message { get; init; }
}

public class StatusValidator
{
    public ValidationResult Validate(Validation validation, TestResult result)
    {
        if (validation is not StatusValidation statusValidation)
        {
            throw new ArgumentException("Validation is not a StatusValidation");
        }
        switch (validation.Target)
        {
            case Target.All:
                foreach (var (conf, responses, _) in  result.Variants)
                {
                    foreach (var response in responses)
                    {
                        if (response.Code != statusValidation.Status)
                        {
                            return new(false)
                            {
                                Message = $"Expected status {statusValidation.Status}, but got {response.Code} for configuration {conf.Name}"
                            };
                        }
                    }
                }
                return new(true);
            case Target.Single single:
                foreach (var (conf, responses, _) in result.Variants)
                {
                    if (single.Index < 0 || single.Index >= responses.Count)
                    {
                        return new(false)
                        {
                            Message = $"Index {single.Index} is out of range for the responses of configurations {conf.Name}"
                        };
                    }
                    var response = responses[single.Index];
                    if (response.Code != statusValidation.Status)
                    {
                        return new(false)
                        {
                            Message = $"Expected status {statusValidation.Status}, but got {response.Code} for responsed index {single.Index} of configuration {conf.Name}"
                        };
                    }
                }
                return new(true);
            case Target.Multiple:
                return ValidateResponse(statusValidation, result);
            default:
                throw new ArgumentException("Invalid target");
        }
        validation.Target



    }
}