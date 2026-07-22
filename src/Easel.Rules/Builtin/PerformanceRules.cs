using Easel.Core.Model;
using Easel.Core.Symbols;
using Easel.Fx;

namespace Easel.Rules.Builtin;

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

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            // Only the loop BODY (last argument) counts — a data op in the source
            // table argument is a normal query, not an N+1.
            if (FormulaPerfPatterns.PerRowDataOp(ctx.Fx.Facts(pr.Property.Formula)))
                yield return Report(
                    "ForAll performs a per-row data operation (N+1 pattern).",
                    pr.Property.Location, pr.Path,
                    help: "Batch the operation: build a table and Patch/Collect once instead of per row.");
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

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            var facts = ctx.Fx.Facts(pr.Property.Formula);
            var reported = false;
            foreach (var q in facts.Calls.Where(c => c.IsAny(FormulaPerfPatterns.QueryFunctions)))
            {
                if (reported) break;
                if (q.Arg(0) is not Microsoft.PowerFx.Syntax.FirstNameNode src) continue;
                var name = src.Ident.Name.Value;
                // Only flag a KNOWN data source. An unknown identifier (control, parameter,
                // component prop…) is not assumed to be a remote source — stay conservative.
                if (!ctx.Symbols.DefinitionsOf(name).Any(d => d.Kind == SymbolKind.DataSource)) continue;

                if (AstMetrics.ContainsCall(q.Node, FormulaPerfPatterns.NonDelegableFunctions))
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

/// <summary>PA1014 — ForAll nested inside another ForAll (quadratic row iteration).</summary>
public sealed class NestedForAllRule : RuleBase
{
    public override string Id => "PA1014";
    public override string Name => "nested-forall";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            var parse = ctx.Fx.Parse(pr.Property.Formula);
            if (parse is not { IsSuccess: true, Root: not null }) continue;
            if (FormulaPerfPatterns.NestedForAll(parse.Root))
                yield return Report(
                    "ForAll nested inside another ForAll — O(n²) row iteration.",
                    pr.Property.Location, pr.Path,
                    help: "Precompute the inner lookup once (With/AddColumns/GroupBy) instead of re-iterating per row.");
        }
    }
}

/// <summary>
/// PA1015 — the same expensive call (LookUp/Filter/Sort/…) repeated verbatim within one
/// formula. Each occurrence re-evaluates the query.
/// </summary>
public sealed class RepeatedExpensiveCallRule : RuleBase
{
    public override string Id => "PA1015";
    public override string Name => "repeated-expensive-call";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var minLength = ctx.Options.Child("min-length").AsInt() ?? 20;
        var minOccurrences = ctx.Options.Child("min-occurrences").AsInt() ?? 2;

        foreach (var pr in ctx.Formulas())
        {
            var facts = ctx.Fx.Facts(pr.Property.Formula);
            foreach (var (source, count) in FormulaPerfPatterns.RepeatedExpensiveCalls(
                         pr.Property.Formula, facts, minLength, minOccurrences))
            {
                var snippet = source.Length > 60 ? source[..57] + "..." : source;
                yield return Report(
                    $"'{snippet}' is evaluated {count}× in this formula.",
                    pr.Property.Location, pr.Path,
                    help: "Evaluate once and reuse: With({result: <call>}, ... result ... result ...).");
            }
        }
    }
}

/// <summary>PA1016 — CountRows(Filter(...)) materialises the filtered table just to count it.</summary>
public sealed class CountRowsFilterRule : RuleBase
{
    public override string Id => "PA1016";
    public override string Name => "countrows-filter";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            if (FormulaPerfPatterns.CountRowsOverFilter(ctx.Fx.Facts(pr.Property.Formula)))
                yield return Report(
                    "CountRows(Filter(...)) — use CountIf(source, condition) instead.",
                    pr.Property.Location, pr.Path,
                    help: "CountIf counts without materialising the filtered table and delegates on more sources.");
        }
    }
}

/// <summary>PA1017 — First(Filter(...)) fetches a whole filtered table to use one row.</summary>
public sealed class FirstFilterRule : RuleBase
{
    public override string Id => "PA1017";
    public override string Name => "first-filter";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            if (FormulaPerfPatterns.FirstOverFilter(ctx.Fx.Facts(pr.Property.Formula)))
                yield return Report(
                    "First(Filter(...)) — use LookUp(source, condition) instead.",
                    pr.Property.Location, pr.Path,
                    help: "LookUp stops at the first match and delegates; First(Filter(...)) may pull many rows.");
        }
    }
}

/// <summary>
/// PA1018 — several Collect/ClearCollect calls run sequentially in a startup/navigation
/// property when Concurrent() could load them in parallel.
/// </summary>
public sealed class SequentialDataLoadRule : RuleBase
{
    public override string Id => "PA1018";
    public override string Name => "sequential-data-loads";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Info;

    private static readonly string[] TriggerProperties = { "OnStart", "OnVisible" };

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var min = ctx.Options.Child("min-loads").AsInt() ?? 2;

        foreach (var pr in ctx.Formulas())
        {
            if (!TriggerProperties.Contains(pr.Property.Name, StringComparer.OrdinalIgnoreCase)) continue;
            var parse = ctx.Fx.Parse(pr.Property.Formula);
            if (parse is not { IsSuccess: true, Root: not null }) continue;

            var loads = FormulaPerfPatterns.SequentialDataLoads(parse.Root);
            if (loads >= min)
                yield return Report(
                    $"{loads} sequential data loads in {pr.Property.Name} — wrap independent ones in Concurrent().",
                    pr.Property.Location, pr.Path,
                    help: "Concurrent(ClearCollect(a, ...), ClearCollect(b, ...)) loads in parallel and cuts startup time.");
        }
    }
}

/// <summary>
/// PA1019 — a formula references a control that lives on another screen, forcing that
/// screen to load eagerly (App checker: "Inefficient delay loading"). Conservative:
/// fires only for names with exactly one definition in the app, so shadowed or reused
/// names never produce a false positive.
/// </summary>
public sealed class InefficientDelayedLoadRule : RuleBase
{
    public override string Id => "PA1019";
    public override string Name => "inefficient-delayed-load";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var def in ctx.Symbols.OfKind(SymbolKind.Control))
        {
            if (def.Scope is null) continue;
            if (ctx.Symbols.DefinitionsOf(def.Name).Count != 1) continue;

            var foreign = ctx.Symbols.Usages(def.Name)
                .Where(u => !string.Equals(u.Scope, def.Scope, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (foreign.Count == 0) continue;

            var where = string.Join(", ", foreign.Select(f => f.InPath).Distinct().Take(3));
            yield return Report(
                $"Control '{def.Name}' on '{def.Scope}' is referenced from {where} — forces '{def.Scope}' to load eagerly.",
                foreign[0].Location, foreign[0].InPath,
                help: "Cross-screen control references defeat delayed load. Share the value via a variable/collection or a Navigate context record.");
        }
    }
}

/// <summary>
/// PA1028 — a Text input feeding a query (Filter/LookUp/Search) without DelayOutput.
/// Every keystroke re-runs the query; DelayOutput batches input into ~half-second
/// pauses (solution checker: app-use-delayoutput-text-input).
/// </summary>
public sealed class DelayOutputTextInputRule : RuleBase
{
    public override string Id => "PA1028";
    public override string Name => "delay-output-text-input";
    public override RuleCategory Category => RuleCategory.Performance;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        // Text inputs that do not opt into DelayOutput, keyed by name.
        var candidates = new Dictionary<string, (string Screen, Control Control)>(StringComparer.OrdinalIgnoreCase);
        foreach (var screen in ctx.Model.Screens)
            foreach (var c in screen.AllControls())
                if (BaseType(c) == "TextInput" && !A11y.IsTrue(c.GetProperty("DelayOutput")))
                    candidates.TryAdd(c.Name, (screen.Name, c));
        if (candidates.Count == 0) yield break;

        var flagged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pr in ctx.Formulas())
        {
            var facts = ctx.Fx.Facts(pr.Property.Formula);
            foreach (var query in facts.Calls.Where(c => c.IsAny(FormulaPerfPatterns.QueryFunctions)))
            {
                foreach (var (left, right) in FxFactsWalker.Collect(query.Node).DottedNames)
                {
                    if (!string.Equals(right, "Text", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!candidates.TryGetValue(left, out var hit) || !flagged.Add(left)) continue;

                    yield return Report(
                        $"Text input '{hit.Control.Name}' drives a query ({pr.Path}) without DelayOutput — the query runs on every keystroke.",
                        hit.Control.Location, $"{hit.Screen}/{hit.Control.Name}",
                        help: "Set DelayOutput to true so the query runs after a typing pause instead of per keystroke.");
                }
            }
        }
    }
}
