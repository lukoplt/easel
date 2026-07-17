namespace PaCheck.Pac;

public enum InputKind
{
    /// <summary>An already-unpacked source folder (read directly, e.g. Git integration).</summary>
    UnpackedFolder,
    /// <summary>A .msapp file — must be unpacked via pac.</summary>
    Msapp,
    /// <summary>A solution .zip containing canvas apps — unpacked via pac.</summary>
    SolutionZip,
    /// <summary>Recognised but in the legacy pre-YAML format — needs a Studio re-save.</summary>
    PreYaml,
    /// <summary>Not a recognised canvas-app input.</summary>
    Unknown,
    /// <summary>Path does not exist.</summary>
    NotFound,
}

public sealed record ResolvedInput(InputKind Kind, string Path, string Message)
{
    public bool IsError => Kind is InputKind.PreYaml or InputKind.Unknown or InputKind.NotFound;
}

/// <summary>Classifies a user-supplied path into how pacheck should ingest it.</summary>
public static class InputResolver
{
    private const string PreYamlHelp =
        "This app appears to be in the legacy pre-YAML source format. " +
        "Open it in Power Apps Studio and re-save so it is exported as pa.yaml, then retry.";

    public static ResolvedInput Resolve(string path)
    {
        if (Directory.Exists(path))
            return ResolveFolder(path);

        if (File.Exists(path))
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".msapp" => new ResolvedInput(InputKind.Msapp, path, "Canvas app package (.msapp)."),
                ".zip" => new ResolvedInput(InputKind.SolutionZip, path, "Solution archive (.zip)."),
                _ => new ResolvedInput(InputKind.Unknown, path, $"Unsupported file type '{ext}'."),
            };
        }

        return new ResolvedInput(InputKind.NotFound, path, $"Path not found: {path}");
    }

    private static ResolvedInput ResolveFolder(string path)
    {
        if (HasPaYaml(path))
            return new ResolvedInput(InputKind.UnpackedFolder, path, "Unpacked pa.yaml source folder.");

        // Legacy unpack produced *.json control files / *.fx.yaml but no *.pa.yaml.
        var looksLegacy =
            Directory.EnumerateFiles(path, "*.fx.yaml", SearchOption.AllDirectories).Any() ||
            Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories)
                .Any(f => f.Contains("Controls", StringComparison.OrdinalIgnoreCase));

        return looksLegacy
            ? new ResolvedInput(InputKind.PreYaml, path, PreYamlHelp)
            : new ResolvedInput(InputKind.Unknown, path, "No pa.yaml sources found in folder.");
    }

    public static bool HasPaYaml(string folder) =>
        Directory.EnumerateFiles(folder, "*.pa.yaml", SearchOption.AllDirectories).Any();
}
