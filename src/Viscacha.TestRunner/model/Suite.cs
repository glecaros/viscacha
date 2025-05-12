using System.Collections.Generic;

namespace Viscacha.TestRunner.Model;

public record Suite(
    Dictionary<string, string>? Variables,
    List<ConfigurationReference> Configurations,
    List<Test> Tests
);