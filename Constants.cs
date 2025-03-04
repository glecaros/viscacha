using System.Text.RegularExpressions;

namespace ApiTester;

public static partial class Constants
{
    [GeneratedRegex(@"\$\{([^}]+)\}")]
    public static partial Regex EnvironmentVariableRegex { get; }

    [GeneratedRegex(@"#\{([^}]+)\}")]
    public static partial Regex ResponseVariableRegex { get; }

    [GeneratedRegex(@"\{\{([^}]+)\}\}")]
    public static partial Regex CommandLineVariableRegex { get; }
}