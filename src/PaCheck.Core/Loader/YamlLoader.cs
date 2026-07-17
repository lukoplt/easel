using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace PaCheck.Core.Loader;

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

        // The unpacked layout puts sources under Src/ (pac) but also support a flat folder.
        var searchRoots = new[] { Path.Combine(root, "Src"), root }
            .Where(Directory.Exists)
            .Distinct()
            .ToArray();
        var searchRoot = searchRoots.FirstOrDefault(root) ?? root;

        var files = Directory
            .EnumerateFiles(searchRoot, "*.pa.yaml", SearchOption.AllDirectories)
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
}
