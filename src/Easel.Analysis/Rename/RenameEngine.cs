using System.Text;
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
/// Renames state symbols and named formulas in unpacked canvas source. Edits are applied to
/// exact YAML scalar source spans, so comments, scalar styles and unrelated formatting remain
/// byte-for-byte unchanged. Structural controls/screens are deliberately unsupported.
/// </summary>
public static class RenameEngine
{
    private static readonly SymbolKind[] Renamable =
        { SymbolKind.GlobalVariable, SymbolKind.ContextVariable, SymbolKind.Collection, SymbolKind.NamedFormula };

    private sealed record TextEdit(int Start, int End, string Replacement, int Occurrences);

    public static RenameResult Rename(string folder, string oldName, string newName, AppAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return new RenameResult(false, "Both --from and --to are required.", 0, 0);
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
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

        var wordRx = new Regex($@"\b{Regex.Escape(oldName)}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var stringHits = analysis.Model.AllProperties()
            .Where(p => p.Property.HasFormula)
            .SelectMany(p => analysis.Fx.Facts(p.Property.Formula).Strings)
            .Count(s => wordRx.IsMatch(s.Value));

        var originals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var filesChanged = 0;
        var total = 0;

        try
        {
            var files = Directory.EnumerateFiles(folder, "*.pa.yaml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(folder, "*.fx.yaml", SearchOption.AllDirectories));

            foreach (var file in files)
            {
                var original = File.ReadAllText(file);
                var (rewritten, occurrences) = RewriteFile(original, oldName, newName);
                if (occurrences == 0) continue;

                originals[file] = original;
                File.WriteAllText(file, rewritten);
                filesChanged++;
                total += occurrences;
            }

            if (total == 0)
                return new RenameResult(false, $"No identifier occurrences of '{oldName}' found to rename.", 0, 0);

            // A successful rename must move every definition, not merely update references.
            var after = AppAnalysis.FromFolder(folder);
            if (after.Symbols.IsDefined(oldName) || after.Symbols.Usages(oldName).Count > 0 ||
                !after.Symbols.IsDefined(newName))
            {
                Restore(originals);
                return new RenameResult(false,
                    $"Rename validation failed: definitions of '{oldName}' were not moved completely.", 0, 0, stringHits);
            }

            return new RenameResult(true, $"Renamed '{oldName}' → '{newName}'.", filesChanged, total, stringHits);
        }
        catch
        {
            Restore(originals);
            throw;
        }
    }

    private static (string Text, int Occurrences) RewriteFile(string source, string oldName, string newName)
    {
        using var reader = new StringReader(source);
        var stream = new YamlStream();
        stream.Load(reader);

        var edits = new List<TextEdit>();
        foreach (var doc in stream.Documents)
            CollectEdits(doc.RootNode, source, oldName, newName, inFormulaMap: false, edits);

        if (edits.Count == 0) return (source, 0);

        var sb = new StringBuilder(source);
        var total = 0;
        var previousStart = source.Length;
        foreach (var edit in edits.OrderByDescending(e => e.Start))
        {
            if (edit.Start < 0 || edit.End < edit.Start || edit.End > source.Length || edit.End > previousStart)
                throw new InvalidOperationException("Overlapping or invalid YAML source spans during rename.");
            sb.Remove(edit.Start, edit.End - edit.Start);
            sb.Insert(edit.Start, edit.Replacement);
            previousStart = edit.Start;
            total += edit.Occurrences;
        }
        return (sb.ToString(), total);
    }

    private static void CollectEdits(YamlNode node, string source, string oldName, string newName,
        bool inFormulaMap, List<TextEdit> edits)
    {
        switch (node)
        {
            case YamlMappingNode map:
                foreach (var (key, value) in map.Children)
                {
                    if (inFormulaMap && key is YamlScalarNode formulaName &&
                        string.Equals(formulaName.Value, oldName, StringComparison.OrdinalIgnoreCase))
                    {
                        var originalKey = SourceSlice(source, formulaName);
                        edits.Add(new TextEdit((int)formulaName.Start.Index, (int)formulaName.End.Index,
                            PreserveYamlKeyStyle(originalKey, newName), 1));
                    }

                    var keyValue = (key as YamlScalarNode)?.Value;
                    CollectEdits(value, source, oldName, newName,
                        string.Equals(keyValue, "Formulas", StringComparison.OrdinalIgnoreCase), edits);
                }
                break;

            case YamlSequenceNode sequence:
                foreach (var item in sequence.Children)
                    CollectEdits(item, source, oldName, newName, inFormulaMap: false, edits);
                break;

            case YamlScalarNode scalar when scalar.Value?.TrimStart().StartsWith('=') == true:
                var original = SourceSlice(source, scalar);
                var (replacement, count) = RewriteYamlFormulaScalar(original, scalar.Style, oldName, newName);
                if (count > 0)
                    edits.Add(new TextEdit((int)scalar.Start.Index, (int)scalar.End.Index, replacement, count));
                break;
        }
    }

    private static string SourceSlice(string source, YamlNode node) =>
        source[(int)node.Start.Index..(int)node.End.Index];

    private static string PreserveYamlKeyStyle(string original, string newName)
    {
        if (original.Length >= 2 && original[0] == '\'' && original[^1] == '\'')
            return $"'{newName}'";
        if (original.Length >= 2 && original[0] == '"' && original[^1] == '"')
            return $"\"{newName}\"";
        return newName;
    }

    private static (string Text, int Occurrences) RewriteYamlFormulaScalar(
        string source, YamlDotNet.Core.ScalarStyle style, string oldName, string newName)
    {
        if (style is YamlDotNet.Core.ScalarStyle.SingleQuoted or YamlDotNet.Core.ScalarStyle.DoubleQuoted &&
            source.Length >= 2)
        {
            var (inner, count) = RewriteFormulaSource(source[1..^1], oldName, newName);
            return ($"{source[0]}{inner}{source[^1]}", count);
        }

        return RewriteFormulaSource(source, oldName, newName);
    }

    /// <summary>
    /// Replace Power Fx identifiers while preserving string literals and both Power Fx/YAML
    /// comments. The input is the exact source slice of one formula scalar, including a block
    /// scalar indicator when present.
    /// </summary>
    private static (string Text, int Occurrences) RewriteFormulaSource(string source, string oldName, string newName)
    {
        var sb = new StringBuilder(source.Length);
        var count = 0;
        var inString = false;
        var inLineComment = false;
        var inBlockComment = false;

        for (var i = 0; i < source.Length;)
        {
            if (inLineComment)
            {
                var c = source[i++];
                sb.Append(c);
                if (c == '\n') inLineComment = false;
                continue;
            }
            if (inBlockComment)
            {
                if (i + 1 < source.Length && source[i] == '*' && source[i + 1] == '/')
                {
                    sb.Append("*/");
                    i += 2;
                    inBlockComment = false;
                }
                else sb.Append(source[i++]);
                continue;
            }
            if (inString)
            {
                var c = source[i++];
                sb.Append(c);
                if (c == '"')
                {
                    if (i < source.Length && source[i] == '"') sb.Append(source[i++]);
                    else inString = false;
                }
                continue;
            }

            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
            {
                sb.Append("//");
                i += 2;
                inLineComment = true;
                continue;
            }
            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '*')
            {
                sb.Append("/*");
                i += 2;
                inBlockComment = true;
                continue;
            }
            if (source[i] == '#')
            {
                sb.Append(source[i++]);
                inLineComment = true;
                continue;
            }
            if (source[i] == '"')
            {
                sb.Append(source[i++]);
                inString = true;
                continue;
            }
            if (IsIdentifierStart(source[i]))
            {
                var start = i++;
                while (i < source.Length && IsIdentifierPart(source[i])) i++;
                var token = source[start..i];
                if (string.Equals(token, oldName, StringComparison.OrdinalIgnoreCase) &&
                    IsSymbolToken(source, start, i))
                {
                    sb.Append(newName);
                    count++;
                }
                else sb.Append(token);
                continue;
            }

            sb.Append(source[i++]);
        }

        return (sb.ToString(), count);
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool IsSymbolToken(string source, int start, int end)
    {
        var previous = PreviousNonWhitespace(source, start - 1);
        if (previous >= 0 && source[previous] == '.') return false; // record/control field access

        var next = NextNonWhitespace(source, end);
        if (next < source.Length && source[next] == '(') return false; // function call
        if (next < source.Length && source[next] == ':')
            return IsUpdateContextRecordKey(source, start); // other record field declarations

        return true;
    }

    private static bool IsUpdateContextRecordKey(string source, int tokenStart)
    {
        var openBrace = source.LastIndexOf('{', tokenStart - 1);
        var closeBrace = source.LastIndexOf('}', tokenStart - 1);
        if (openBrace < 0 || closeBrace > openBrace) return false;

        var beforeBrace = PreviousNonWhitespace(source, openBrace - 1);
        if (beforeBrace < 0 || source[beforeBrace] != '(') return false;
        var callEnd = PreviousNonWhitespace(source, beforeBrace - 1) + 1;
        var callStart = callEnd;
        while (callStart > 0 && IsIdentifierPart(source[callStart - 1])) callStart--;
        return source[callStart..callEnd].Equals("UpdateContext", StringComparison.OrdinalIgnoreCase);
    }

    private static int PreviousNonWhitespace(string source, int index)
    {
        while (index >= 0 && char.IsWhiteSpace(source[index])) index--;
        return index;
    }

    private static int NextNonWhitespace(string source, int index)
    {
        while (index < source.Length && char.IsWhiteSpace(source[index])) index++;
        return index;
    }

    private static void Restore(IReadOnlyDictionary<string, string> originals)
    {
        foreach (var (file, content) in originals)
            File.WriteAllText(file, content);
    }

    private static bool IsValidIdentifier(string name) =>
        Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$");
}
