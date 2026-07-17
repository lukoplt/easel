using YamlDotNet.Serialization;

namespace PaCheck.Core.Config;

/// <summary>Finds and parses <c>.pacheck.yml</c>, searching upward from a start folder.</summary>
public static class ConfigLoader
{
    public static readonly string[] FileNames = { ".pacheck.yml", ".pacheck.yaml" };

    /// <summary>Locate the nearest config file walking up from <paramref name="start"/>.</summary>
    public static string? Find(string start)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(start));
        // If start is a file, begin at its directory.
        if (File.Exists(start)) dir = new FileInfo(start).Directory;

        while (dir is not null)
        {
            foreach (var name in FileNames)
            {
                var candidate = Path.Combine(dir.FullName, name);
                if (File.Exists(candidate)) return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }

    public static PaCheckConfig Load(string? path)
    {
        if (path is null || !File.Exists(path)) return PaCheckConfig.Empty;
        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder().Build();
            var raw = deserializer.Deserialize<object?>(yaml);
            return new PaCheckConfig(new ConfigNode(raw));
        }
        catch
        {
            // A malformed config falls back to defaults rather than aborting the run.
            return PaCheckConfig.Empty;
        }
    }

    public static PaCheckConfig LoadNearest(string start) => Load(Find(start));
}
