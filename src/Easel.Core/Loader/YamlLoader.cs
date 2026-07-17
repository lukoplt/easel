using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Easel.Core.Loader;

/// <summary>A parsed *.pa.yaml document plus its repo-relative path.</summary>
public sealed record LoadedYamlDoc(string RelativePath, YamlMappingNode Root);

/// <summary>Diagnostic raised while loading YAML (malformed file, etc.).</summary>
public sealed record LoadDiagnostic(string RelativePath, string Message, int Line, int Column);

public sealed record YamlLoadResult(
    IReadOnlyList<LoadedYamlDoc> Docs,
    IReadOnlyList<LoadDiagnostic> Diagnostics,
    string RootPath);

/// <summary>
/// Reads *.pa.yaml source files from an unpacked canvas app folder into
/// YamlDotNet mapping nodes (with source marks). Tolerant: a broken file yields a
/// diagnostic, never an exception that aborts the whole load.
/// </summary>
public static class YamlLoader
{
    public static YamlLoadResult LoadFolder(string root)
    {
        var docs = new List<LoadedYamlDoc>();
        var diags = new List<LoadDiagnostic>();

        // pa.yaml sources can live at Src/ (flat folders) or Other/Src/ (pac solution
        // unpack, which also emits legacy *.fx.yaml alongside). Search the whole tree for
        // *.pa.yaml and skip editor-state files, which are metadata, not app content.
        var files = Directory
            .EnumerateFiles(root, "*.pa.yaml", SearchOption.AllDirectories)
            .Where(f => !IsEditorState(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(root, file);
            try
            {
                using var reader = new StreamReader(file);
                var stream = new YamlStream();
                stream.Load(reader);
                foreach (var doc in stream.Documents)
                {
                    if (doc.RootNode is YamlMappingNode map)
                        docs.Add(new LoadedYamlDoc(rel, map));
                }
            }
            catch (YamlException ye)
            {
                diags.Add(new LoadDiagnostic(rel, ye.Message, (int)ye.Start.Line, (int)ye.Start.Column));
            }
            catch (Exception ex)
            {
                diags.Add(new LoadDiagnostic(rel, ex.Message, 0, 0));
            }
        }

        return new YamlLoadResult(docs, diags, root);
    }

    /// <summary>Editor-state files hold Studio metadata, not app content — exclude them.</summary>
    private static bool IsEditorState(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("_EditorState", StringComparison.OrdinalIgnoreCase)
            || name.Contains("editorstate", StringComparison.OrdinalIgnoreCase)
            || path.Replace('\\', '/').Contains("/EditorState/", StringComparison.OrdinalIgnoreCase);
    }
}
