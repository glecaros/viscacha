using System.Collections.Generic;

namespace Viscacha.CLI.Test.Model;

public record ConfigurationReference(
    string Name,
    string Path,
    Dictionary<string, string>? Variables
);
