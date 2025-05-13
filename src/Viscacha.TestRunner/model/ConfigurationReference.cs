using System.Collections.Generic;

namespace Viscacha.TestRunner.Model;

public record ConfigurationReference(
    string Name,
    string Path,
    Dictionary<string, string>? Variables
);
