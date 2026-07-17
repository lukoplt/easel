using PaCheck.Core.Model;
using PaCheck.Core.Symbols;
using PaCheck.Fx;

namespace PaCheck.Rules.Builtin;

/// <summary>PA1005 — a screen carries more controls than the configured limit.</summary>
public sealed class ScreenControlLimitRule : RuleBase
{
    public override string Id => "PA1005";
    public override string Name => "screen-control-limit";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var max = ctx.Options.Child("max").AsInt() ?? 300;
        foreach (var screen in ctx.Model.Screens)
        {
            var count = screen.AllControls().Count();
            if (count > max)
                yield return Report(
                    $"Screen '{screen.Name}' has {count} controls (limit {max}).",
                    screen.Location, screen.Name,
                    help: "Split into multiple screens or components to keep load time down.");
        }
    }
}

/// <summary>PA1006 — heavy logic in App.OnStart that would be better as named formulas.</summary>
public sealed class HeavyOnStartRule : RuleBase
{
    public override string Id => "PA1006";
    public override string Name => "heavy-onstart";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var maxAssignments = ctx.Options.Child("max-assignments").AsInt() ?? 6;
        var maxNodes = ctx.Options.Child("max-nodes").AsInt() ?? 80;

        var onStart = ctx.Model.App.GetProperty("OnStart");
        if (onStart is not { HasFormula: true }) yield break;

        var parse = ctx.Fx.Parse(onStart.Formula);
        if (parse is not { IsSuccess: true, Root: not null }) yield break;

        var facts = ctx.Fx.Facts(onStart.Formula);
        var assignments = facts.Writes.Count;
        var nodes = AstMetrics.NodeCount(parse.Root);

        if (assignments > maxAssignments || nodes > maxNodes)
            yield return Report(
                $"App.OnStart is heavy ({assignments} assignments, {nodes} nodes).",
                onStart.Location, "App.OnStart",
                help: "Move derived values to named formulas (App.Formulas) so they evaluate lazily.");
    }
}

/// <summary>
/// PA1002 — N+1 pattern: a per-row loop that performs a data operation on each iteration.
/// </summary>
public sealed class NPlusOneRule : RuleBase
{
    public override string Id => "PA1002";
    public override string Name => "n-plus-one";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Warning;

    private static readonly string[] DataOps = { "Patch", "Collect", "Remove", "RemoveIf", "LookUp", "Update", "UpdateIf" };

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            var facts = ctx.Fx.Facts(pr.Property.Formula);
            var reported = false;
            foreach (var forAll in facts.Calls.Where(c => c.Name is "ForAll"))
            {
                if (reported) break;
                if (AstMetrics.ContainsCall(forAll.Node, DataOps))
                {
                    reported = true;
                    yield return Report(
                        "ForAll performs a per-row data operation (N+1 pattern).",
                        pr.Property.Location, pr.Path,
                        help: "Batch the operation: build a table and Patch/Collect once instead of per row.");
                }
            }
        }
    }
}

/// <summary>
/// PA1001 — a non-delegable function used inside a query over a data source.
/// Conservative: fires only when a known non-delegable function sits directly inside
/// Filter/LookUp/Search over an identifier that is a data source (not a local collection).
/// </summary>
public sealed class DelegationRule : RuleBase
{
    public override string Id => "PA1001";
    public override string Name => "non-delegable-query";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Warning;

    private static readonly string[] QueryFns = { "Filter", "LookUp", "Search" };
    private static readonly string[] NonDelegable =
        { "Concat", "CountRows", "Last", "GroupBy", "Ungroup", "ForAll", "Split", "MatchAll" };

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            var facts = ctx.Fx.Facts(pr.Property.Formula);
            var reported = false;
            foreach (var q in facts.Calls.Where(c => QueryFns.Contains(c.Name)))
            {
                if (reported) break;
                // First arg must be a bare data source (not a known collection/variable).
                if (q.Arg(0) is not Microsoft.PowerFx.Syntax.FirstNameNode src) continue;
                var name = src.Ident.Name.Value;
                var def = ctx.Symbols.DefinitionsOf(name).FirstOrDefault();
                var isLocalData = def is { Kind: SymbolKind.Collection or SymbolKind.GlobalVariable or SymbolKind.ContextVariable };
                if (isLocalData) continue; // in-memory, delegation not relevant

                if (AstMetrics.ContainsCall(q.Node, NonDelegable))
                {
                    reported = true;
                    yield return Report(
                        $"{q.Name} over '{name}' may not delegate (non-delegable function in the query).",
                        pr.Property.Location, pr.Path,
                        help: "Non-delegable operations run only over the first 500–2000 rows. Pre-filter server-side.");
                }
            }
        }
    }
}
