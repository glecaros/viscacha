using System.Collections.Generic;

namespace Viscacha.Model.Test;

public record ConfigurationReference(
    string Name,
    string Path,
    Dictionary<string, string>? Variables
);
