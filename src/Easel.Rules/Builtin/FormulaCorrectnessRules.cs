using Easel.Core.Model;
using Easel.Core.Symbols;
using Easel.Fx;
using Microsoft.PowerFx.Syntax;

namespace Easel.Rules.Builtin;

/// <summary>
/// PF0002 — Navigate/SetFocus targeting a name that is defined nowhere in the app.
/// The App checker reports this as a formula error; without live binding easel checks
/// the two navigation functions whose first argument must be an app element.
/// </summary>
public sealed class UnknownNavigationTargetRule : RuleBase
{
    public override string Id => "PF0002";
    public override string Name => "unknown-navigation-target";
    public override RuleCategory Category => RuleCategory.Error;
    public override Severity DefaultSeverity => Severity.Error;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            var facts = ctx.Fx.Facts(pr.Property.Formula);
            foreach (var call in facts.Calls.Where(c => c.IsAny("Navigate", "SetFocus")))
            {
                if (call.Arg(0) is not FirstNameNode target) continue;
                var name = target.Ident.Name.Value;
                if (ctx.Symbols.IsDefined(name)) continue;
                if (FxFunctionCatalog.BuiltinIdentifiers.Contains(name)) continue;

                yield return Report(
                    $"{call.Name}({name}, …) — '{name}' is not defined anywhere in the app.",
                    pr.Property.Location, pr.Path,
                    help: call.Is("Navigate")
                        ? "Navigate needs an existing screen. Check the screen name for typos."
                        : "SetFocus needs an existing control. Check the control name for typos.");
            }
        }
    }
}

/// <summary>
/// PF0003 — an identifier that is defined nowhere but is one edit away from a defined
/// symbol: almost certainly a typo (txtSerch.Text → txtSearch). Names the formula
/// introduces itself (With fields, As aliases) and built-in identifiers are exempt.
/// </summary>
public sealed class PossibleTypoRule : RuleBase
{
    public override string Id => "PF0003";
    public override string Name => "possible-typo";
    public override RuleCategory Category => RuleCategory.Error;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var defined = ctx.Symbols.Definitions
            .Select(d => d.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var pr in ctx.Formulas())
        {
            var parse = ctx.Fx.Parse(pr.Property.Formula);
            if (parse is not { IsSuccess: true, Root: not null }) continue;

            var facts = ctx.Fx.Facts(pr.Property.Formula);
            var scopeNames = FxScopeNames.Collect(parse.Root);
            var writeTargets = facts.Writes.Select(w => (w.Name, w.TargetSpanStart)).ToHashSet();
            var classifier = FxNameClassifier.Collect(parse.Root);
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var n in facts.FirstNames)
            {
                if (n.Name.Length < 4 || !reported.Add(n.Name)) continue;
                if (writeTargets.Contains((n.Name, n.SpanStart))) continue;
                if (scopeNames.Contains(n.Name)) continue;
                if (ctx.Symbols.IsDefined(n.Name)) continue;
                if (FxFunctionCatalog.BuiltinIdentifiers.Contains(n.Name)) continue;
                // An entity-column mention (bare inside Filter/LookUp/… scope, or a
                // DataSourceInfo-style column argument) has a schema easel cannot see —
                // skip. A dotted base (name.Prop) or free-standing name stays checked.
                if (!classifier.IsStructuralReference(n.Name)) continue;

                var maxDistance = n.Name.Length >= 7 ? 2 : 1;
                var match = defined.FirstOrDefault(d =>
                    Math.Abs(d.Length - n.Name.Length) <= maxDistance
                    && !d.Equals(n.Name, StringComparison.OrdinalIgnoreCase)
                    && !IsPluralPair(n.Name, d)
                    && Levenshtein(n.Name, d, maxDistance) <= maxDistance);
                if (match is null) continue;

                yield return Report(
                    $"'{n.Name}' is not defined — did you mean '{match}'?",
                    pr.Property.Location, pr.Path,
                    help: "The name resolves to nothing in this app; one edit away from a defined symbol suggests a typo.");
            }
        }
    }

    /// <summary>
    /// 'Operator' vs 'Operators' is a column referencing its own table's plural — a
    /// legitimate record-scope field, not a typo. Skip singular/plural pairs.
    /// </summary>
    public static bool IsPluralPair(string a, string b) =>
        string.Equals(a + "s", b, StringComparison.OrdinalIgnoreCase)
        || string.Equals(b + "s", a, StringComparison.OrdinalIgnoreCase);

    /// <summary>Bounded Levenshtein distance (case-insensitive), early-exits above <paramref name="max"/>.</summary>
    public static int Levenshtein(string a, string b, int max)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            var rowMin = cur[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                rowMin = Math.Min(rowMin, cur[j]);
            }
            if (rowMin > max) return max + 1;
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }
}

/// <summary>
/// PF0004 — a built-in function called with an argument count outside its documented
/// signature. Bounds come from a curated catalog; unknown functions are never flagged.
/// </summary>
public sealed class WrongArgumentCountRule : RuleBase
{
    public override string Id => "PF0004";
    public override string Name => "wrong-argument-count";
    public override RuleCategory Category => RuleCategory.Error;
    public override Severity DefaultSeverity => Severity.Error;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            foreach (var call in ctx.Fx.Facts(pr.Property.Formula).Calls)
            {
                if (call.Namespace is not null) continue;
                if (FxFunctionCatalog.ArityOf(call.Name) is not { } arity) continue;
                var (min, max) = arity;
                if (call.Args.Count >= min && call.Args.Count <= max) continue;

                var expected = max == FxFunctionCatalog.Variadic ? $"at least {min}"
                    : min == max ? $"{min}"
                    : $"{min}–{max}";
                yield return Report(
                    $"{call.Name} takes {expected} argument(s), got {call.Args.Count}.",
                    pr.Property.Location, pr.Path,
                    help: "Check the function's signature in the Power Fx formula reference.");
            }
        }
    }
}

/// <summary>
/// PF0005 — a call to a function that is neither a known built-in nor defined in the
/// app (named formula / user-defined function). Namespaced calls are skipped. Extra
/// known-good names can be configured under `allow`.
/// </summary>
public sealed class UnknownFunctionRule : RuleBase
{
    public override string Id => "PF0005";
    public override string Name => "unknown-function";
    public override RuleCategory Category => RuleCategory.Error;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var allow = new HashSet<string>(ctx.Options.Child("allow").AsStringList(), StringComparer.OrdinalIgnoreCase);

        foreach (var pr in ctx.Formulas())
        {
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var call in ctx.Fx.Facts(pr.Property.Formula).Calls)
            {
                if (call.Namespace is not null) continue;
                if (FxFunctionCatalog.IsKnown(call.Name)) continue;
                if (ctx.Symbols.IsDefined(call.Name)) continue; // user-defined function / named formula
                if (allow.Contains(call.Name) || !reported.Add(call.Name)) continue;

                yield return Report(
                    $"'{call.Name}' is not a known function.",
                    pr.Property.Location, pr.Path,
                    help: "Check the spelling against the Power Fx formula reference, or add the name under this rule's `allow` list if it is intentional.");
            }
        }
    }
}
