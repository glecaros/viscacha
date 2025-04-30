using System.Collections.Generic;

namespace Viscacha.Model.Test;

public record Test(
    string Name,
    Dictionary<string, string>? Variables,
    string RequestFile,
    List<string> Configurations,
    List<ValidationDefinition> Validations,
    bool? Skip
);
