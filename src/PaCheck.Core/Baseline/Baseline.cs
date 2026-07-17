using System.Text.Json;
using System.Text.Json.Serialization;
using PaCheck.Core.Model;

namespace PaCheck.Core.Baseline;

/// <summary>
/// A set of accepted-finding fingerprints. Lets teams adopt pacheck on a legacy app and
/// only fail on newly-introduced findings. Fingerprints are line-independent (see
/// <see cref="Finding.Fingerprint"/>) so they survive unrelated edits.
/// </summary>
public sealed class Baseline
{
    public const string DefaultFileName = ".pacheck-baseline.json";

    private readonly HashSet<string> _fingerprints;

    public Baseline(IEnumerable<string> fingerprints) =>
        _fingerprints = new HashSet<string>(fingerprints, StringComparer.Ordinal);

    public int Count => _fingerprints.Count;

    public bool Contains(Finding f) => _fingerprints.Contains(f.Fingerprint());

    /// <summary>Findings not present in the baseline (the "new" ones).</summary>
    public IReadOnlyList<Finding> Filter(IEnumerable<Finding> findings) =>
        findings.Where(f => !Contains(f)).ToList();

    public static Baseline FromFindings(IEnumerable<Finding> findings) =>
        new(findings.Select(f => f.Fingerprint()));

    public void Save(string path)
    {
        var dto = new BaselineFile
        {
            Fingerprints = _fingerprints.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        };
        File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOpts));
    }

    public static Baseline Load(string path)
    {
        if (!File.Exists(path)) return new Baseline(Array.Empty<string>());
        try
        {
            var dto = JsonSerializer.Deserialize<BaselineFile>(File.ReadAllText(path), JsonOpts);
            return new Baseline(dto?.Fingerprints ?? new List<string>());
        }
        catch
        {
            return new Baseline(Array.Empty<string>());
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private sealed class BaselineFile
    {
        [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = "1.0";
        [JsonPropertyName("fingerprints")] public List<string> Fingerprints { get; set; } = new();
    }
}
