using Microsoft.PowerFx.Syntax;

namespace Easel.Fx;

/// <summary>
/// Pure AST predicates for Power Fx performance anti-patterns. Shared between the
/// performance rules (scan) and the fix validator (`easel fix`), so a suggested fix
/// can be re-checked with exactly the detection logic that produced the finding.
/// </summary>
public static class FormulaPerfPatterns
{
    /// <summary>Functions whose per-row use inside a ForAll body is an N+1 pattern.</summary>
    public static readonly string[] PerRowDataOps =
        { "Patch", "Collect", "Remove", "RemoveIf", "LookUp", "Update", "UpdateIf" };

    /// <summary>Query functions whose delegation can silently break.</summary>
    public static readonly string[] QueryFunctions = { "Filter", "LookUp", "Search" };

    /// <summary>Functions that never delegate inside a query over a remote source.</summary>
    public static readonly string[] NonDelegableFunctions =
        { "Concat", "CountRows", "Last", "GroupBy", "Ungroup", "ForAll", "Split", "MatchAll" };

    /// <summary>Calls expensive enough that repeating them verbatim warrants a With().</summary>
    public static readonly string[] ExpensiveCalls =
    {
        "LookUp", "Filter", "Search", "Sort", "SortByColumns", "GroupBy", "AddColumns",
        "Distinct", "CountRows", "CountIf", "Sum", "Average", "Max", "Min", "Concat",
    };

    /// <summary>ForAll nested inside another ForAll — O(n²) row iteration.</summary>
    public static bool NestedForAll(TexlNode root) =>
        AstMetrics.MaxCallNesting(root, "ForAll") >= 2;

    /// <summary>A ForAll whose BODY (last argument) performs a per-row data operation.</summary>
    public static bool PerRowDataOp(FxFacts facts)
    {
        foreach (var forAll in facts.Calls.Where(c => c.Is("ForAll")))
        {
            var body = forAll.Arg(forAll.Args.Count - 1);
            if (body is not null && forAll.Args.Count >= 2 && AstMetrics.ContainsCall(body, PerRowDataOps))
                return true;
        }
        return false;
    }

    /// <summary>A Filter/LookUp/Search that contains a non-delegable function anywhere inside.</summary>
    public static bool NonDelegableQueryShape(FxFacts facts) =>
        facts.Calls.Any(c => c.IsAny(QueryFunctions) && AstMetrics.ContainsCall(c.Node, NonDelegableFunctions));

    /// <summary>CountRows(Filter(...)) — counts by materialising the filtered table.</summary>
    public static bool CountRowsOverFilter(FxFacts facts) =>
        facts.Calls.Any(c => c.Is("CountRows") && c.Arg(0) is CallNode inner
                             && string.Equals(inner.Head.Name.Value, "Filter", StringComparison.OrdinalIgnoreCase));

    /// <summary>First(Filter(...)) — fetches a whole filtered table to use one row.</summary>
    public static bool FirstOverFilter(FxFacts facts) =>
        facts.Calls.Any(c => c.Is("First") && c.Arg(0) is CallNode inner
                             && string.Equals(inner.Head.Name.Value, "Filter", StringComparison.OrdinalIgnoreCase));

    /// <summary>Count of Collect/ClearCollect calls that are NOT inside a Concurrent(...).</summary>
    public static int SequentialDataLoads(TexlNode root)
    {
        var v = new SequentialLoadVisitor();
        root.Accept(v);
        return v.Count;
    }

    /// <summary>
    /// Identical expensive calls repeated within one formula. Returns each repeated
    /// call's source text and occurrence count. Spans index into
    /// <paramref name="formula"/> — the exact string that was parsed.
    /// </summary>
    public static IReadOnlyList<(string Source, int Count)> RepeatedExpensiveCalls(
        string formula, FxFacts facts, int minLength = 20, int minOccurrences = 2)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var call in facts.Calls.Where(c => c.IsAny(ExpensiveCalls)))
        {
            var span = call.Node.GetCompleteSpan();
            if (span.Min < 0 || span.Lim > formula.Length || span.Lim <= span.Min) continue;
            var source = NormalizeWhitespace(formula[span.Min..span.Lim]);
            if (source.Length < minLength) continue;
            counts[source] = counts.TryGetValue(source, out var n) ? n + 1 : 1;
        }
        return counts.Where(kv => kv.Value >= minOccurrences)
                     .Select(kv => (kv.Key, kv.Value))
                     .OrderBy(x => x.Key, StringComparer.Ordinal)
                     .ToList();
    }

    private static string NormalizeWhitespace(string s) =>
        string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed class SequentialLoadVisitor : IdentityTexlVisitor
    {
        private int _concurrentDepth;
        public int Count { get; private set; }

        public override bool PreVisit(CallNode node)
        {
            var name = node.Head.Name.Value;
            if (string.Equals(name, "Concurrent", StringComparison.OrdinalIgnoreCase))
                _concurrentDepth++;
            else if (_concurrentDepth == 0
                     && (string.Equals(name, "Collect", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(name, "ClearCollect", StringComparison.OrdinalIgnoreCase)))
                Count++;
            return true;
        }

        public override void PostVisit(CallNode node)
        {
            if (string.Equals(node.Head.Name.Value, "Concurrent", StringComparison.OrdinalIgnoreCase))
                _concurrentDepth--;
        }
    }
}
