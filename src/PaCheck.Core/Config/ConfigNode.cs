using System.Globalization;

namespace PaCheck.Core.Config;

/// <summary>
/// Tolerant read-only view over a deserialised YAML value (scalar / map / list).
/// Never throws on a missing or mistyped key — returns <see cref="Empty"/> or null.
/// </summary>
public sealed class ConfigNode
{
    public static readonly ConfigNode Empty = new(null);
    private readonly object? _raw;

    public ConfigNode(object? raw) => _raw = raw;

    public bool Exists => _raw is not null;

    public string? AsString() => _raw switch
    {
        null => null,
        string s => s,
        _ => Convert.ToString(_raw, CultureInfo.InvariantCulture),
    };

    public int? AsInt() =>
        int.TryParse(AsString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    public double? AsDouble() =>
        double.TryParse(AsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    public bool? AsBool()
    {
        var s = AsString()?.Trim().ToLowerInvariant();
        return s switch
        {
            "true" or "yes" or "on" => true,
            "false" or "no" or "off" => false,
            _ => null,
        };
    }

    /// <summary>Child by case-insensitive key. Returns <see cref="Empty"/> when absent.</summary>
    public ConfigNode Child(string key)
    {
        if (_raw is IDictionary<object, object> map)
        {
            foreach (var kv in map)
            {
                if (kv.Key is string k && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return new ConfigNode(kv.Value);
            }
        }
        return Empty;
    }

    public IEnumerable<string> AsStringList()
    {
        if (_raw is IEnumerable<object> list)
            foreach (var item in list)
                if (item is not null)
                    yield return Convert.ToString(item, CultureInfo.InvariantCulture) ?? "";
    }

    public IEnumerable<(string Key, ConfigNode Value)> AsMap()
    {
        if (_raw is IDictionary<object, object> map)
            foreach (var kv in map)
                if (kv.Key is string k)
                    yield return (k, new ConfigNode(kv.Value));
    }
}
