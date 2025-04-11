using System.Collections.Generic;

namespace Viscacha.Model.Test;

public record Suite(
    Dictionary<string, string>? Variables,
    List<ConfigurationReference> Configurations,
    List<Test> Tests
);