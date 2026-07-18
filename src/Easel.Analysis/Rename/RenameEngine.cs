using System.Text.RegularExpressions;
using Easel.Core;
using Easel.Core.Symbols;
using YamlDotNet.RepresentationModel;

namespace Easel.Analysis.Rename;

public sealed record RenameResult(
    bool Success,
    string Message,
    int FilesChanged,
    int Occurrences,
    int StringLiteralHits = 0);

/// <summary>
/// Renames an identifier across an unpacked source folder (preview). AST-precise: it only
/// rewrites Power Fx identifier tokens inside <c>=</c>-prefixed formula values, so string
/// literals, comments and YAML keys are never touched. Structural renames (controls/screens)
/// are refused because those live as YAML keys. Operates in place on the given folder —
/// callers pass a temp copy, never the original input.
/// </summary>
public static class RenameEngine
{
    private static readonly SymbolKind[] Renamable =
        { SymbolKind.GlobalVariable, SymbolKind.ContextVariable, SymbolKind.Collection, SymbolKind.NamedFormula };

    public static RenameResult Rename(string folder, string oldName, string newName, AppAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return new RenameResult(false, "Both --from and --to are required.", 0, 0);
        if (string.Equals(oldName, newName, StringComparison.Ordinal))
            return new RenameResult(false, "--from and --to are identical.", 0, 0);
        if (!IsValidIdentifier(newName))
            return new RenameResult(false, $"'{newName}' is not a valid identifier.", 0, 0);

        var defs = analysis.Symbols.DefinitionsOf(oldName);
        if (defs.Count == 0)
            return new RenameResult(false, $"Symbol '{oldName}' is not defined in this app.", 0, 0);
        if (analysis.Symbols.IsDefined(newName))
            return new RenameResult(false, $"Collision: '{newName}' is already defined. Choose another name.", 0, 0);
        if (!defs.All(d => Renamable.Contains(d.Kind)))
            return new RenameResult(false,
                $"'{oldName}' is a {defs.First(d => !Renamable.Contains(d.Kind)).Kind}. Preview rename supports variables, collections and named formulas only.", 0, 0);

        // Occurrences that live inside string literals are, by construction, NOT touched by
        // the AST-precise rewrite — report them so the user knows they were intentionally left.
        var wordRx = new Regex($@"\b{Regex.Escape(oldName)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        int stringHits = analysis.Model.AllProperties()
            .Where(p => p.Property.HasFormula)
            .SelectMany(p => analysis.Fx.Facts(p.Property.Formula).Strings)
            .Count(s => wordRx.IsMatch(s.Value));

        int filesChanged = 0, total = 0;
        var files = Directory.EnumerateFiles(folder, "*.pa.yaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(folder, "*.fx.yaml", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            int fileCount;
            try
            {
                fileCount = RewriteFile(file, oldName, newName, analysis);
            }
            catch (YamlDotNet.Core.YamlException)
            {
                continue; // a malformed source file is skipped, not fatal
            }
            if (fileCount == 0) continue;
            filesChanged++;
            total += fileCount;
        }

        if (total == 0)
            return new RenameResult(false, $"No identifier occurrences of '{oldName}' found to rename.", 0, 0);

        return new RenameResult(true, $"Renamed '{oldName}' → '{newName}'.", filesChanged, total, stringHits);
    }

    private static int RewriteFile(string file, string oldName, string newName, AppAnalysis analysis)
    {
        using var reader = new StreamReader(file);
        var stream = new YamlStream();
        stream.Load(reader);
        reader.Close();

        int count = 0;
        foreach (var doc in stream.Documents)
            foreach (var scalar in Scalars(doc.RootNode))
                count += RewriteScalar(scalar, oldName, newName, analysis);

        if (count == 0) return 0;

        using var writer = new StreamWriter(file, append: false);
        stream.Save(writer, assignAnchors: false);
        return count;
    }

    /// <summary>Rewrite identifier tokens inside a single '='-prefixed formula scalar.</summary>
    private static int RewriteScalar(YamlScalarNode scalar, string oldName, string newName, AppAnalysis analysis)
    {
        var raw = scalar.Value;
        if (raw is null) return 0;
        var trimmed = raw.TrimStart();
        if (!trimmed.StartsWith('=')) return 0;   // only formula values start with '='

        var formula = trimmed[1..];
        var spans = analysis.Fx.Facts(formula).FirstNames
            .Where(n => string.Equals(n.Name, oldName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.SpanStart)   // replace right-to-left to keep offsets valid
            .ToList();
        if (spans.Count == 0) return 0;

        foreach (var s in spans)
        {
            if (s.SpanStart < 0 || s.SpanEnd > formula.Length || s.SpanStart >= s.SpanEnd) continue;
            formula = formula[..s.SpanStart] + newName + formula[s.SpanEnd..];
        }

        scalar.Value = "=" + formula;
        return spans.Count;
    }

    private static IEnumerable<YamlScalarNode> Scalars(YamlNode node)
    {
        switch (node)
        {
            case YamlScalarNode s:
                yield return s;
                break;
            case YamlMappingNode m:
                foreach (var kv in m.Children)
                {
                    // Keys are never rewritten (they don't start with '='), but recurse for completeness.
                    foreach (var x in Scalars(kv.Key)) yield return x;
                    foreach (var x in Scalars(kv.Value)) yield return x;
                }
                break;
            case YamlSequenceNode seq:
                foreach (var item in seq.Children)
                    foreach (var x in Scalars(item))
                        yield return x;
                break;
        }
    }

    private static bool IsValidIdentifier(string name) =>
        Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$");
}
