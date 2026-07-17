using System.Reflection;

namespace PaCheck.Cli.Infrastructure;

public static class ToolInfo
{
    public const string ToolName = "pacheck";

    public static string Version =>
        typeof(ToolInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            is { Length: > 0 } v
            ? v.Split('+')[0]
            : typeof(ToolInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>The pa.yaml schema generation this build targets.</summary>
    public const string SupportedPaYamlSchema = "3.0";
}
