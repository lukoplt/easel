using Easel.Fx;

namespace Easel.Cli.Ai;

/// <summary>
/// Re-checks a suggested formula against the same AST pattern that produced the finding.
/// A fix that still exhibits the anti-pattern is flagged so the user discards it.
/// </summary>
public static class FixValidator
{
    /// <summary>
    /// True if the suggested formula still triggers the rule's pattern, false if the
    /// pattern is gone, null when the rule has no formula-local pattern to re-check
    /// (e.g. it needs app-wide context like the symbol table).
    /// </summary>
    public static bool? StillTriggers(string ruleId, string formula, FxParseService fx)
    {
        var parse = fx.Parse(formula);
        if (parse is not { IsSuccess: true, Root: not null }) return null;
        var facts = fx.Facts(formula);

        return ruleId.ToUpperInvariant() switch
        {
            // PA1001 proper needs the symbol table; the query SHAPE is checkable locally
            // and a valid fix removes it, so re-check the shape conservatively.
            "PA1001" => FormulaPerfPatterns.NonDelegableQueryShape(facts),
            "PA1002" => FormulaPerfPatterns.PerRowDataOp(facts),
            "PA1014" => FormulaPerfPatterns.NestedForAll(parse.Root),
            "PA1015" => FormulaPerfPatterns.RepeatedExpensiveCalls(formula, facts).Count > 0,
            "PA1016" => FormulaPerfPatterns.CountRowsOverFilter(facts),
            "PA1017" => FormulaPerfPatterns.FirstOverFilter(facts),
            "PA1018" => FormulaPerfPatterns.SequentialDataLoads(parse.Root) >= 2,
            _ => null,
        };
    }
}
