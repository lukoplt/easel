using System.Text.RegularExpressions;
using PaCheck.Core;

namespace PaCheck.Analysis.Rename;

public sealed record RenameResult(
    bool Success,
    string Message,
    int FilesChanged,
    int Occurrences,
    int StringLiteralHits = 0);

/// <summary>
/// Renames an identifier across an unpacked source folder (preview). Whole-word matching
/// over pa.yaml / fx.yaml text, with a pre-flight collision check against the symbol table.
/// Operates in place on the given folder — callers pass a temp copy, never the original input.
///
/// Whole-word matching avoids substring hits (varPopup ≠ varPopupVisible). It cannot,
/// however, tell an identifier apart from the same word inside a string literal, so those
/// occurrences are counted and reported for the user to verify (rename is preview).
/// </summary>
public static class RenameEngine
{
    public static RenameResult Rename(string folder, string oldName, string newName, AppAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            return new RenameResult(false, "Both --from and --to are required.", 0, 0);
        if (string.Equals(oldName, newName, StringComparison.Ordinal))
            return new RenameResult(false, "--from and --to are identical.", 0, 0);
        if (!IsValidIdentifier(newName))
            return new RenameResult(false, $"'{newName}' is not a valid identifier.", 0, 0);

        if (!analysis.Symbols.IsDefined(oldName))
            return new RenameResult(false, $"Symbol '{oldName}' is not defined in this app.", 0, 0);
        if (analysis.Symbols.IsDefined(newName))
            return new RenameResult(false, $"Collision: '{newName}' is already defined. Choose another name.", 0, 0);

        var rx = new Regex($@"\b{Regex.Escape(oldName)}\b");

        // Count occurrences that live inside string literals (from the parsed model, so it is
        // accurate) — these will also be text-replaced and warrant a look.
        int stringHits = analysis.Model.AllProperties()
            .Where(p => p.Property.HasFormula)
            .SelectMany(p => analysis.Fx.Facts(p.Property.Formula).Strings)
            .Count(s => rx.IsMatch(s.Value));

        int filesChanged = 0, total = 0;
        var files = Directory.EnumerateFiles(folder, "*.pa.yaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(folder, "*.fx.yaml", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            int count = rx.Matches(text).Count;
            if (count == 0) continue;
            File.WriteAllText(file, rx.Replace(text, newName));
            filesChanged++;
            total += count;
        }

        return new RenameResult(true, $"Renamed '{oldName}' → '{newName}'.", filesChanged, total, stringHits);
    }

    private static bool IsValidIdentifier(string name) =>
        Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$");
}
