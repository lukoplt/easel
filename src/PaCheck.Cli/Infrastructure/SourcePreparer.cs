using PaCheck.Pac;

namespace PaCheck.Cli.Infrastructure;

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
                pac.UnpackMsapp(path, dest, line => log?.Invoke(line));
                return new PreparedSource(dest, resolved.Kind, path, isTemp: true, keep: keepTemp);
            }

            case InputKind.SolutionZip:
                throw new InputException(
                    "Solution .zip inputs are not supported yet. Extract the canvas .msapp and pass it directly.");

            default:
                throw new InputException(resolved.Message);
        }
    }
}
