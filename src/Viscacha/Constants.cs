using System.Text.RegularExpressions;

namespace Viscacha;

public static partial class Constants
{
    [GeneratedRegex(@"\$\{([^}]+)\}")]
    public static partial Regex EnvironmentVariableRegex { get; }

    [GeneratedRegex(@"#\{([^}]+)\}")]
    public static partial Regex ResponseVariableRegex { get; }
}