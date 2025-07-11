using System.Collections.Generic;

namespace Viscacha.CLI.Test.Model;

public record Test(
    string Name,
    Dictionary<string, string>? Variables,
    string RequestFile,
    List<string> Configurations,
    List<ValidationDefinition> Validations,
    bool? Skip
);
