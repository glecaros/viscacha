using System.Collections.Generic;

namespace Viscacha.CLI.Test.Model;

public record Suite(
    Dictionary<string, string>? Variables,
    List<ConfigurationReference> Configurations,
    List<Test> Tests
);
