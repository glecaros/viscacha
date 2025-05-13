using System.Collections.Generic;

namespace Viscacha.TestRunner.Model;

public record Test(
    string Name,
    Dictionary<string, string>? Variables,
    string RequestFile,
    List<string> Configurations,
    List<ValidationDefinition> Validations,
    bool? Skip
);
