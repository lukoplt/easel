using Easel.Core.Model;
using Easel.Core.Symbols;

namespace Easel.Rules.Builtin;

/// <summary>PA1003 — a variable or collection is written but never read.</summary>
public sealed class UnusedVariableRule : RuleBase
{
    public override string Id => "PA1003";
    public override string Name => "unused-variable";
    public override RuleCategory Category => RuleCategory.Maintainability;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var kinds = new[] { SymbolKind.GlobalVariable, SymbolKind.ContextVariable, SymbolKind.Collection };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in ctx.Symbols.Definitions.Where(d => kinds.Contains(d.Kind)))
        {
            var identity = def.Kind == SymbolKind.ContextVariable
                ? $"{def.Kind}:{def.Scope}:{def.Name}"
                : $"{def.Kind}:{def.Name}";
            if (!seen.Add(identity)) continue;
            if (ctx.Symbols.ReadCount(def) > 0) continue;

            var noun = def.Kind == SymbolKind.Collection ? "Collection" : "Variable";
            yield return Report(
                $"{noun} '{def.Name}' is assigned but never read.",
                def.Location, def.DefinedInPath,
                help: "Remove the assignment, or reference the value where it is needed.");
        }
    }
}

/// <summary>PA1004 — a media asset is present but never referenced.</summary>
public sealed class UnusedMediaRule : RuleBase
{
    public override string Id => "PA1004";
    public override string Name => "unused-media";
    public override RuleCategory Category => RuleCategory.Maintainability;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        if (ctx.Model.Media.Count == 0) yield break;

        // Collect every string literal across all formulas once.
        var literals = new List<string>();
        foreach (var pr in ctx.Formulas())
            literals.AddRange(ctx.Fx.Facts(pr.Property.Formula).Strings.Select(s => s.Value));

        foreach (var media in ctx.Model.Media)
        {
            var byIdentifier = ctx.Symbols.ReadCount(media.Name) > 0;
            var byString = literals.Any(l =>
                ContainsToken(l, media.Name) ||
                (media.FileName is not null && ContainsToken(l, media.FileName)));

            if (!byIdentifier && !byString)
                yield return Report(
                    $"Media asset '{media.FileName ?? media.Name}' is not referenced anywhere.",
                    media.Location, media.Name,
                    help: "Remove the unused asset to shrink the app package.");
        }
    }

    /// <summary>
    /// Whole-token containment: 'logo' matches in "https://x/logo.png" but not in
    /// "logotype". A bare substring test masked genuinely unused media (App checker
    /// parity: it reports assets easel used to consider referenced).
    /// </summary>
    public static bool ContainsToken(string text, string token)
    {
        if (token.Length == 0 || token.Length > text.Length) return false;
        var idx = 0;
        while ((idx = text.IndexOf(token, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            var afterIdx = idx + token.Length;
            var afterOk = afterIdx >= text.Length || !char.IsLetterOrDigit(text[afterIdx]);
            if (beforeOk && afterOk) return true;
            idx++;
        }
        return false;
    }
}

/// <summary>
/// PA1027 — a screen that nothing references. Fires only when App.StartScreen is set:
/// with a known start screen every reachable screen is referenced somewhere (Navigate,
/// StartScreen, or a formula), so a zero-reference screen is provably dead weight.
/// </summary>
public sealed class UnusedScreenRule : RuleBase
{
    public override string Id => "PA1027";
    public override string Name => "unused-screen";
    public override RuleCategory Category => RuleCategory.Maintainability;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        if (ctx.Model.Screens.Count < 2) yield break;
        if (ctx.Model.App.GetProperty("StartScreen") is not { HasFormula: true }) yield break;

        foreach (var screen in ctx.Model.Screens)
        {
            if (ctx.Symbols.ReadCount(screen.Name) > 0) continue;
            yield return Report(
                $"Screen '{screen.Name}' is never navigated to or referenced.",
                screen.Location, screen.Name,
                help: "Remove the screen, or add the Navigate(...) call that should reach it. Unused screens still cost load time.");
        }
    }
}

/// <summary>PA1012 — the same non-trivial formula is duplicated across the app.</summary>
public sealed class DuplicateFormulaRule : RuleBase
{
    public override string Id => "PA1012";
    public override string Name => "duplicate-formula";
    public override RuleCategory Category => RuleCategory.Maintainability;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var minLength = ctx.Options.Child("min-length").AsInt() ?? 40;
        var minOccurrences = ctx.Options.Child("min-occurrences").AsInt() ?? 3;

        var groups = ctx.Formulas()
            .Where(p => p.Property.Formula.Length >= minLength)
            .GroupBy(p => Normalize(p.Property.Formula))
            .Where(g => g.Count() >= minOccurrences);

        foreach (var g in groups)
        {
            var first = g.First();
            var where = string.Join(", ", g.Take(4).Select(p => p.Path));
            yield return Report(
                $"Formula repeated {g.Count()} times ({where}{(g.Count() > 4 ? ", …" : "")}).",
                first.Property.Location, first.Path,
                help: "Extract into a named formula or a component to define it once.");
        }
    }

    private static string Normalize(string f) =>
        string.Concat(f.Where(c => !char.IsWhiteSpace(c)));
}

/// <summary>PA1013 — the same control type is used at different versions across the app.</summary>
public sealed class InconsistentControlVersionRule : RuleBase
{
    public override string Id => "PA1013";
    public override string Name => "inconsistent-control-version";
    public override RuleCategory Category => RuleCategory.Maintainability;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var byType = ctx.Model.AllControls()
            .Where(c => c.Version is not null)
            .GroupBy(c => c.ControlType, StringComparer.OrdinalIgnoreCase);

        foreach (var g in byType)
        {
            var versions = g.Select(c => c.Version!).Distinct().OrderBy(v => v).ToList();
            if (versions.Count <= 1) continue;

            var first = g.First();
            yield return Report(
                $"Control '{g.Key}' appears at {versions.Count} versions: {string.Join(", ", versions)}.",
                first.Location, first.Name,
                help: "Align on a single control version to avoid inconsistent behaviour.");
        }
    }
}
