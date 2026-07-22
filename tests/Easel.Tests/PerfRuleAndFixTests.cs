using Easel.Cli.Ai;
using Easel.Fx;
using Xunit;

namespace Easel.Tests;

/// <summary>
/// Unit tests for the formula-level performance patterns (PA1014–PA1018) and the
/// `easel fix` side: guidance exists for each pattern and the validator confirms the
/// prescribed rewrite actually removes the anti-pattern.
/// </summary>
public sealed class PerfRuleAndFixTests
{
    private readonly FxParseService _fx = new();

    private (Microsoft.PowerFx.Syntax.TexlNode Root, FxFacts Facts) Parse(string formula)
    {
        var p = _fx.Parse(formula);
        Assert.True(p.IsSuccess, $"fixture formula must parse: {formula}");
        return (p.Root!, _fx.Facts(formula));
    }

    // --- pattern detection ---------------------------------------------------------

    [Fact]
    public void Nested_forall_detected_and_single_forall_ignored()
    {
        var (nested, _) = Parse("ForAll(a, ForAll(b, Collect(c, {V: Value})))");
        Assert.True(FormulaPerfPatterns.NestedForAll(nested));

        var (single, _) = Parse("ForAll(a, Collect(c, {V: Value}))");
        Assert.False(FormulaPerfPatterns.NestedForAll(single));
    }

    [Fact]
    public void Countrows_over_filter_detected_and_countif_ignored()
    {
        Assert.True(FormulaPerfPatterns.CountRowsOverFilter(Parse("CountRows(Filter(t, x > 1))").Facts));
        Assert.False(FormulaPerfPatterns.CountRowsOverFilter(Parse("CountIf(t, x > 1)").Facts));
        Assert.False(FormulaPerfPatterns.CountRowsOverFilter(Parse("CountRows(t)").Facts));
    }

    [Fact]
    public void First_over_filter_detected_and_lookup_ignored()
    {
        Assert.True(FormulaPerfPatterns.FirstOverFilter(Parse("First(Filter(t, x > 1)).Name").Facts));
        Assert.False(FormulaPerfPatterns.FirstOverFilter(Parse("LookUp(t, x > 1, Name)").Facts));
        Assert.False(FormulaPerfPatterns.FirstOverFilter(Parse("First(t).Name").Facts));
    }

    [Fact]
    public void Sequential_loads_counted_only_outside_concurrent()
    {
        var (seq, _) = Parse("ClearCollect(a, S1); ClearCollect(b, S2); Set(x, 1)");
        Assert.Equal(2, FormulaPerfPatterns.SequentialDataLoads(seq));

        var (conc, _) = Parse("Concurrent(ClearCollect(a, S1), ClearCollect(b, S2)); Set(x, 1)");
        Assert.Equal(0, FormulaPerfPatterns.SequentialDataLoads(conc));

        var (mixed, _) = Parse("Concurrent(ClearCollect(a, S1), ClearCollect(b, S2)); Collect(c, S3)");
        Assert.Equal(1, FormulaPerfPatterns.SequentialDataLoads(mixed));
    }

    [Fact]
    public void Repeated_expensive_call_found_with_and_without_whitespace_variance()
    {
        var formula = "If(LookUp(colX, Value = \"Open\", Value) = \"Open\", LookUp(colX,  Value = \"Open\", Value), \"n\")";
        var facts = _fx.Facts(formula);
        var repeats = FormulaPerfPatterns.RepeatedExpensiveCalls(formula, facts);
        Assert.Single(repeats);
        Assert.Equal(2, repeats[0].Count);
    }

    [Fact]
    public void Short_or_single_expensive_calls_not_reported()
    {
        var once = "If(LookUp(colX, Value = \"Open\", Value) = \"Open\", \"a\", \"n\")";
        Assert.Empty(FormulaPerfPatterns.RepeatedExpensiveCalls(once, _fx.Facts(once)));

        var shortCall = "Sum(a, V) + Sum(a, V)"; // below min length
        Assert.Empty(FormulaPerfPatterns.RepeatedExpensiveCalls(shortCall, _fx.Facts(shortCall)));
    }

    // --- fix procedures + validation ------------------------------------------------

    [Theory]
    [InlineData("PA1001")]
    [InlineData("PA1002")]
    [InlineData("PA1006")]
    [InlineData("PA1014")]
    [InlineData("PA1015")]
    [InlineData("PA1016")]
    [InlineData("PA1017")]
    [InlineData("PA1018")]
    public void Every_perf_rule_has_a_fix_procedure(string ruleId)
    {
        Assert.False(string.IsNullOrWhiteSpace(FixGuidance.For(ruleId)));
    }

    [Theory]
    // broken formula, formula after applying the prescribed fix procedure
    [InlineData("PA1002",
        "ForAll(items, Patch(DS, Defaults(DS), {Name: Value}))",
        "Patch(DS, ForAll(items, {Name: Value}))")]
    [InlineData("PA1014",
        "ForAll(a, ForAll(b, Collect(c, {V: Value})))",
        "With({pairs: AddColumns(a, S, b)}, Collect(c, pairs))")]
    [InlineData("PA1015",
        "If(LookUp(colX, Value = \"Open\", Value) = \"Open\", LookUp(colX, Value = \"Open\", Value), \"n\")",
        "With({r: LookUp(colX, Value = \"Open\", Value)}, If(r = \"Open\", r, \"n\"))")]
    [InlineData("PA1016",
        "CountRows(Filter(t, Status = \"Open\"))",
        "CountIf(t, Status = \"Open\")")]
    [InlineData("PA1017",
        "First(Filter(t, Status = \"Open\")).Name",
        "LookUp(t, Status = \"Open\", Name)")]
    [InlineData("PA1018",
        "ClearCollect(a, S1); ClearCollect(b, S2)",
        "Concurrent(ClearCollect(a, S1), ClearCollect(b, S2))")]
    public void Validator_flags_broken_formula_and_passes_fixed_one(string ruleId, string broken, string fixedFormula)
    {
        Assert.True(FixValidator.StillTriggers(ruleId, broken, _fx));
        Assert.False(FixValidator.StillTriggers(ruleId, fixedFormula, _fx));
    }

    [Fact]
    public void Validator_returns_null_for_rules_without_local_pattern()
    {
        Assert.Null(FixValidator.StillTriggers("PA1009", "Button1.Text", _fx));
    }

    [Fact]
    public void Validator_returns_null_for_unparsable_suggestion()
    {
        Assert.Null(FixValidator.StillTriggers("PA1016", "CountRows(Filter(t,", _fx));
    }
}
