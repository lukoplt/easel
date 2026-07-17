using Easel.Core.Model;

namespace Easel.Core.Config;

/// <summary>Per-rule configuration slice handed to a rule at evaluation time.</summary>
public sealed class RuleConfig
{
    public static readonly RuleConfig Default = new(true, null, ConfigNode.Empty);

    public bool Enabled { get; }
    public Severity? SeverityOverride { get; }
    public ConfigNode Options { get; }

    public RuleConfig(bool enabled, Severity? severityOverride, ConfigNode options)
    {
        Enabled = enabled;
        SeverityOverride = severityOverride;
        Options = options;
    }
}

/// <summary>
/// Resolved <c>.easel.yml</c> configuration. Keyed lookups are by rule name or id.
/// </summary>
public sealed class EaselConfig
{
    private readonly ConfigNode _root;

    public IReadOnlyList<string> Ignore { get; }
    public string OutputFormat { get; }

    public static EaselConfig Empty { get; } = new(ConfigNode.Empty);

    public EaselConfig(ConfigNode root)
    {
        _root = root;
        Ignore = root.Child("ignore").AsStringList().ToList();
        OutputFormat = root.Child("output").Child("format").AsString() ?? "console";
    }

    /// <summary>Configuration for a rule, resolved by id first then name.</summary>
    public RuleConfig ForRule(string id, string name)
    {
        var rules = _root.Child("rules");
        var node = rules.Child(id);
        if (!node.Exists) node = rules.Child(name);
        if (!node.Exists) return RuleConfig.Default;

        // A bare "off" / boolean value disables the rule.
        var bare = node.AsBool();
        if (bare == false) return new RuleConfig(false, null, ConfigNode.Empty);
        if (string.Equals(node.AsString(), "off", StringComparison.OrdinalIgnoreCase))
            return new RuleConfig(false, null, ConfigNode.Empty);

        var sevStr = node.Child("severity").AsString();
        Severity? sev = ParseSeverity(sevStr, out var disabled);
        return new RuleConfig(!disabled, sev, node);
    }

    private static Severity? ParseSeverity(string? s, out bool disabled)
    {
        disabled = false;
        switch (s?.Trim().ToLowerInvariant())
        {
            case null or "": return null;
            case "off" or "none": disabled = true; return null;
            case "info" or "hint": return Severity.Info;
            case "warning" or "warn": return Severity.Warning;
            case "error": return Severity.Error;
            default: return null;
        }
    }
}
