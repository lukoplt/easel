using Easel.Pac;

namespace Easel.Cli.Infrastructure;

/// <summary>
/// An unpacked source folder ready for analysis, plus metadata about where it came from.
/// Disposing removes the temp folder (unless --keep-temp).
/// </summary>
public sealed class PreparedSource : IDisposable
{
    public string Folder { get; }
    public InputKind OriginalKind { get; }
    public string OriginalPath { get; }
    public bool IsTemp { get; }
    private readonly bool _keep;

    internal PreparedSource(string folder, InputKind kind, string originalPath, bool isTemp, bool keep)
    {
        Folder = folder;
        OriginalKind = kind;
        OriginalPath = originalPath;
        IsTemp = isTemp;
        _keep = keep;
    }

    public void Dispose()
    {
        if (IsTemp && !_keep && Directory.Exists(Folder))
        {
            try { Directory.Delete(Folder, recursive: true); } catch { /* best effort */ }
        }
    }
}

/// <summary>Turns any supported input into an unpacked source folder, using pac when needed.</summary>
public static class SourcePreparer
{
    public static PreparedSource Prepare(string path, bool keepTemp, Action<string>? log = null)
    {
        var resolved = InputResolver.Resolve(path);

        switch (resolved.Kind)
        {
            case InputKind.UnpackedFolder:
                return new PreparedSource(path, resolved.Kind, path, isTemp: false, keep: true);

            case InputKind.Msapp:
            {
                var pac = PacRunner.Create(); // throws PacException (exit 3) if unavailable
                var dest = PacRunner.TempFolderFor(path);
                if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
                log?.Invoke($"Unpacking {Path.GetFileName(path)} via pac…");
                try
                {
                    pac.UnpackMsapp(path, dest, line => log?.Invoke(line));
                }
                catch
                {
                    TryDelete(dest);   // never leave a half-unpacked temp behind
                    throw;
                }
                return new PreparedSource(dest, resolved.Kind, path, isTemp: true, keep: keepTemp);
            }

            case InputKind.SolutionZip:
            {
                var pac = PacRunner.Create();
                var extractDir = PacRunner.TempFolderFor(path) + "-sol";
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
                log?.Invoke($"Extracting solution {Path.GetFileName(path)}…");
                try
                {
                    SafeZip.Extract(path, extractDir);   // guards against zip bombs / zip slip

                    var msapps = Directory
                        .EnumerateFiles(extractDir, "*.msapp", SearchOption.AllDirectories)
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (msapps.Count == 0)
                        throw new InputException("No canvas app (.msapp) found in the solution.");
                    if (msapps.Count > 1)
                        throw new InputException(
                            $"Solution contains {msapps.Count} canvas apps. Pass one directly:\n  " +
                            string.Join("\n  ", msapps));

                    var dest = PacRunner.TempFolderFor(msapps[0]);
                    if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
                    log?.Invoke($"Unpacking {Path.GetFileName(msapps[0])} via pac…");
                    try
                    {
                        pac.UnpackMsapp(msapps[0], dest, line => log?.Invoke(line));
                    }
                    catch
                    {
                        TryDelete(dest);
                        throw;
                    }
                    return new PreparedSource(dest, resolved.Kind, path, isTemp: true, keep: keepTemp);
                }
                finally
                {
                    TryDelete(extractDir);   // the extracted solution is always disposable
                }
            }

            default:
                throw new InputException(resolved.Message);
        }
    }

    private static void TryDelete(string dir)
    {
        if (Directory.Exists(dir))
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
    }
}
