using System.IO.Compression;

namespace Easel.Cli.Infrastructure;

/// <summary>
/// Guarded zip extraction. Protects against zip bombs (entry count / total size / per-entry
/// size / compression ratio) and against zip-slip (entries escaping the target directory).
/// </summary>
public static class SafeZip
{
    // Solution packages are modest; these ceilings are generous but bound the blast radius.
    private const long MaxTotalBytes = 1L * 1024 * 1024 * 1024; // 1 GB uncompressed
    private const long MaxEntryBytes = 256L * 1024 * 1024;      // 256 MB per file
    private const int MaxEntries = 20_000;
    private const long MinBytesForRatioCheck = 1024;           // ignore ratio on tiny files
    private const double MaxCompressionRatio = 200.0;          // uncompressed/compressed

    public static void Extract(string zipPath, string destination)
    {
        Directory.CreateDirectory(destination);
        var destRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaxEntries)
            throw new InputException($"Solution has too many entries ({archive.Entries.Count} > {MaxEntries}).");

        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!targetPath.StartsWith(destRoot, StringComparison.Ordinal))
                throw new InputException($"Refusing zip entry outside the target folder: {entry.FullName}");

            // Directory entry.
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            if (entry.Length > MaxEntryBytes)
                throw new InputException($"Zip entry too large: {entry.FullName} ({entry.Length} bytes).");
            if (entry.CompressedLength >= MinBytesForRatioCheck &&
                (double)entry.Length / entry.CompressedLength > MaxCompressionRatio)
                throw new InputException($"Suspicious compression ratio for {entry.FullName} (possible zip bomb).");

            total += entry.Length;
            if (total > MaxTotalBytes)
                throw new InputException($"Solution expands beyond the {MaxTotalBytes / (1024 * 1024)} MB limit (possible zip bomb).");

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }
}
