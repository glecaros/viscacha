using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Azure;
using Viscacha.Model.Test;
using Viscacha.Runner;

namespace Viscacha.TestRunner.Runner.Validator;

public record ValidationResult(bool Success)
{
    public string? Message { get; init; }
}

public class StatusValidator
{
    internal static ValidationResult Validate(Validation validation, TestResult result)
    {
        if (validation is not StatusValidation statusValidation)
        {
            throw new ArgumentException("Validation is not a StatusValidation");
        }
        foreach (var (conf, responses, _) in  result.Variants)
        {
            switch (validation.Target)
            {
                case Target.All:
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
                    break;
                }
                case Target.Single single:
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
                    break;
                }
                case Target.Multiple multiple:
                {
                    var badIndexCount = multiple.Indices.Where(i => i < 0 || i >= responses.Count).Count();
                    if (badIndexCount > 0)
                    {
                        return new(false)
                        {
                            Message = $"{badIndexCount} indices are out of range for the responses of configuration {conf.Name}"
                        };
                    }
                    var responsesToCheck = multiple.Indices.Select(i => (Index: i, Response: responses[i]));
                    foreach (var (index, response) in responsesToCheck)
                    {
                        if (response.Code != statusValidation.Status)
                        {
                            return new(false)
                            {
                                Message = $"Expected status {statusValidation.Status}, but got {response.Code} for responsed index {index} of configuration {conf.Name}"
                            };
                        }
                    }
                    break;
                }
            }
        }
        return new(true);
    }
}