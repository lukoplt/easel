namespace Easel.Cli.Ai;

/// <summary>
/// Deterministic per-rule repair procedures. Injected into the `easel fix` prompt so the
/// model follows a known-good rewrite recipe instead of improvising, and shown to the
/// user in dry-run mode.
/// </summary>
public static class FixGuidance
{
    private static readonly Dictionary<string, string> Procedures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PA1001"] =
            "Keep only delegable predicates (=, <>, <, >, StartsWith on indexed columns) inside " +
            "Filter/LookUp/Search over the data source. Move non-delegable work (Concat, CountRows, " +
            "Last, GroupBy, ForAll, Split, MatchAll) outside the query: pre-filter server-side first, " +
            "then apply the non-delegable function to the small result, e.g. " +
            "With({rows: Filter(DS, Status = \"Open\")}, Concat(rows, Name, \", \")).",
        ["PA1002"] =
            "Replace the per-row data operation inside ForAll with one batched call: " +
            "build the record table with ForAll and pass it to a single Patch/Collect, e.g. " +
            "Patch(DS, ForAll(items, {Name: Value})) or Collect(target, ForAll(items, {...})). " +
            "One round-trip instead of N.",
        ["PA1006"] =
            "Move derived values out of App.OnStart into App.Formulas named formulas (they evaluate " +
            "lazily and cache). Keep OnStart only for true side effects; wrap independent data loads " +
            "in Concurrent().",
        ["PA1014"] =
            "Remove the inner ForAll: precompute what it derives once with With/AddColumns/GroupBy " +
            "before the outer loop, then reference the precomputed value per row. The result must " +
            "iterate each table once, never rows × rows.",
        ["PA1015"] =
            "Wrap the repeated call in With so it evaluates once: " +
            "With({result: <the repeated call>}, <formula with every duplicate replaced by result>). " +
            "Keep the call text identical to the original in the With binding.",
        ["PA1016"] =
            "Rewrite CountRows(Filter(source, condition)) as CountIf(source, condition). " +
            "Keep the condition unchanged.",
        ["PA1017"] =
            "Rewrite First(Filter(source, condition)) as LookUp(source, condition). " +
            "A trailing field access moves onto LookUp's third argument: " +
            "First(Filter(t, p)).Col becomes LookUp(t, p, Col).",
        ["PA1018"] =
            "Wrap the independent Collect/ClearCollect calls in a single Concurrent(...): " +
            "Concurrent(ClearCollect(a, ...), ClearCollect(b, ...)). Only group loads that do not " +
            "depend on each other's results; keep dependent loads sequential after the Concurrent.",
    };

    /// <summary>The repair procedure for a rule, or null when none is defined.</summary>
    public static string? For(string ruleId) =>
        Procedures.TryGetValue(ruleId, out var p) ? p : null;
}
