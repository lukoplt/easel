using PaCheck.Core.Model;
using YamlDotNet.RepresentationModel;

namespace PaCheck.Core.Loader;

/// <summary>Helpers over YamlDotNet's representation model that also carry source marks.</summary>
public static class YamlSource
{
    public static SourceLocation Location(this YamlNode node, string file) =>
        new(file, (int)node.Start.Line, (int)node.Start.Column);

    public static YamlMappingNode? AsMap(this YamlNode? node) => node as YamlMappingNode;
    public static YamlSequenceNode? AsSeq(this YamlNode? node) => node as YamlSequenceNode;

    public static string? ScalarValue(this YamlNode? node) =>
        node is YamlScalarNode s ? s.Value : null;

    /// <summary>Child value by case-insensitive key, or null.</summary>
    public static YamlNode? Child(this YamlMappingNode map, string key)
    {
        foreach (var kv in map.Children)
        {
            if (kv.Key is YamlScalarNode k &&
                string.Equals(k.Value, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }
        return null;
    }

    /// <summary>Enumerate (name, valueNode) pairs of a mapping, preserving order.</summary>
    public static IEnumerable<(string Name, YamlNode Value, SourceLocation Loc)> Entries(
        this YamlMappingNode map, string file)
    {
        foreach (var kv in map.Children)
        {
            if (kv.Key is YamlScalarNode k && k.Value is not null)
                yield return (k.Value, kv.Value, k.Location(file));
        }
    }

    /// <summary>
    /// Interpret a YAML scalar as a Power Apps property value. Strips the leading '='
    /// that marks a formula and reports whether it was present.
    /// </summary>
    public static Property ToProperty(string name, YamlNode value, string file)
    {
        var loc = value.Location(file);
        var raw = value.ScalarValue() ?? string.Empty;
        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith('='))
            return new Property(name, trimmed[1..].Trim(), IsFormula: true, loc);

        // Literal scalar (no '='). Kept as-is; not a formula.
        return new Property(name, raw.Trim(), IsFormula: false, loc);
    }
}
