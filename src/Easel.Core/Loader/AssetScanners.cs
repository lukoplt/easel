using Easel.Core.Model;

namespace Easel.Core.Loader;

/// <summary>Best-effort discovery of media assets from an unpacked app folder.</summary>
public static class MediaScanner
{
    private static readonly string[] MediaExtensions =
        { ".png", ".jpg", ".jpeg", ".gif", ".svg", ".bmp", ".ico", ".mp4", ".mp3", ".wav", ".webp" };

    public static IReadOnlyList<MediaAsset> Scan(string root)
    {
        var dirs = new[] { "Assets", Path.Combine("Src", "Assets"), "Resources" }
            .Select(d => Path.Combine(root, d))
            .Where(Directory.Exists);

        var media = new List<MediaAsset>();
        foreach (var dir in dirs)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!MediaExtensions.Contains(ext)) continue;
                var info = new FileInfo(file);
                var rel = Path.GetRelativePath(root, file);
                media.Add(new MediaAsset(
                    Name: Path.GetFileNameWithoutExtension(file),
                    FileName: Path.GetFileName(file),
                    Kind: ext.TrimStart('.'),
                    SizeBytes: info.Exists ? info.Length : null,
                    Location: new SourceLocation(rel, 0, 0)));
            }
        }
        return media;
    }
}

/// <summary>
/// Best-effort discovery of data sources from an unpacked app folder.
/// pac stores these as JSON; we read the file names as source names (tolerant).
/// </summary>
public static class DataSourceScanner
{
    public static IReadOnlyList<DataSource> Scan(string root)
    {
        var dirs = new[] { "DataSources", Path.Combine("Src", "DataSources"), "Connections" }
            .Select(d => Path.Combine(root, d))
            .Where(Directory.Exists);

        var sources = new List<DataSource>();
        foreach (var dir in dirs)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                var rel = Path.GetRelativePath(root, file);
                sources.Add(new DataSource(
                    Name: Path.GetFileNameWithoutExtension(file),
                    Type: null,
                    ConnectorId: null,
                    Location: new SourceLocation(rel, 0, 0)));
            }
        }
        return sources;
    }
}
