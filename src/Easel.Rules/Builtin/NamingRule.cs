using System.Text.RegularExpressions;
using Easel.Core.Model;
using Easel.Core.Symbols;

namespace Easel.Rules.Builtin;

/// <summary>
/// PA1007 — naming conventions. Opt-in: fires only for the element classes that have a
/// configured regex, so it never produces false positives out of the box.
/// </summary>
public sealed class NamingConventionRule : RuleBase
{
    public override string Id => "PA1007";
    public override string Name => "naming-convention";
    public override RuleCategory Category => RuleCategory.Naming;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var patterns = ctx.Options.Child("patterns");
        if (!patterns.Exists) yield break;

        var map = new (string Key, SymbolKind[] Kinds)[]
        {
            ("variable", new[] { SymbolKind.GlobalVariable, SymbolKind.ContextVariable }),
            ("collection", new[] { SymbolKind.Collection }),
            ("screen", new[] { SymbolKind.Screen }),
            ("control", new[] { SymbolKind.Control }),
        };

        foreach (var (key, kinds) in map)
        {
            var pattern = patterns.Child(key).AsString();
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            Regex rx;
            try { rx = new Regex(pattern, RegexOptions.CultureInvariant); }
            catch { continue; } // ignore an invalid user regex rather than crash

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in ctx.Symbols.Definitions.Where(d => kinds.Contains(d.Kind)))
            {
                if (!seen.Add(def.Name)) continue;
                if (rx.IsMatch(def.Name)) continue;
                yield return Report(
                    $"{key} '{def.Name}' does not match naming pattern /{pattern}/.",
                    def.Location, def.DefinedInPath,
                    help: "Rename to follow the team convention.");
            }
        }
    }
}
