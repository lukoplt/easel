using Easel.Fx;
using Xunit;
using Xunit.Abstractions;

namespace Easel.Tests.Spikes;

/// <summary>
/// T0.1 — go/no-go spike. Proves Microsoft.PowerFx parses a spread of real
/// Power Apps formulas on net10.0 outside a host, and yields a walkable AST.
/// </summary>
public sealed class PowerFxParseSpike
{
    private readonly ITestOutputHelper _out;
    public PowerFxParseSpike(ITestOutputHelper output) => _out = output;

    // A spread of real-world Power Apps behaviour + data formulas.
    public static readonly string[] ValidFormulas =
    {
        "Set(gblUser, User().FullName)",
        "UpdateContext({locShow: true, locCount: locCount + 1})",
        "ClearCollect(colItems, Filter(Products, Price > 100))",
        "Collect(colLog, {Time: Now(), Msg: \"hi\"})",
        "Patch(Accounts, LookUp(Accounts, Id = varId), {Name: txtName.Text})",
        "ForAll(colRows, Patch(Orders, Defaults(Orders), {Qty: Value}))",
        "If(varMode = \"edit\", Navigate(scrEdit, ScreenTransition.Fade), Navigate(scrView))",
        "Switch(varStatus, \"A\", \"Active\", \"I\", \"Inactive\", \"Unknown\")",
        "SortByColumns(Filter(Employees, Department = drpDept.Selected.Value), \"LastName\", Ascending)",
        "With({r: LookUp(Products, ProductID = varId)}, r.Name & \" — \" & Text(r.Price, \"[$-en-US]$#,##0.00\"))",
        "CountRows(Filter(colTasks, Status <> \"Done\" && DueDate < Today()))",
        "Concat(colTags, Title, \", \")",
        "Sum(Filter(Sales, Year = 2026), Amount)",
        "IfError(1/varDivisor, Notify(\"div by zero\", NotificationType.Error))",
        "RGBA(0, 120, 212, 1)",
        "Round(varTotal * 1.21, 2)",
        "Text(Now(), \"yyyy-mm-dd hh:mm\")",
        "First(Sort(Gallery1.AllItems, Modified, SortOrder.Descending)).Title",
        "Trim(Substitute(Lower(txtEmail.Text), \" \", \"\"))",
        "Set(varItems, AddColumns(colBase, \"Total\", Qty * Price))",
    };

    public static readonly string[] InvalidFormulas =
    {
        "Set(x,",                 // unterminated call
        "If(a, b",                // missing paren
        "1 + + 2",                // dangling operator
        "Filter(t, )",            // empty arg where expr expected
    };

    public static IEnumerable<object[]> ValidCases() => ValidFormulas.Select(f => new object[] { f });
    public static IEnumerable<object[]> InvalidCases() => InvalidFormulas.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(ValidCases))]
    public void Valid_formula_parses_to_ast(string formula)
    {
        var parser = new PowerFxParser();
        var r = parser.Parse(formula);

        Assert.True(r.IsSuccess, $"expected success for: {formula}\n  errors: {string.Join("; ", r.Errors.Select(e => e.Message))}");
        Assert.NotNull(r.Root);
        _out.WriteLine($"OK  {formula}\n    root: {r.Root!.GetType().Name}");
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Invalid_formula_fails_gracefully(string formula)
    {
        var parser = new PowerFxParser();
        var r = parser.Parse(formula);

        // Must not throw; must surface as failure with diagnostics (feeds PF0001).
        Assert.False(r.IsSuccess, $"expected failure for: {formula}");
        Assert.NotEmpty(r.Errors);
        _out.WriteLine($"FAIL(as expected)  {formula}\n    {string.Join("; ", r.Errors.Select(e => e.Message))}");
    }

    [Fact]
    public void Batch_parse_reports_go_no_go_summary()
    {
        var parser = new PowerFxParser();
        int ok = ValidFormulas.Count(f => parser.Parse(f).IsSuccess);
        _out.WriteLine($"go/no-go: {ok}/{ValidFormulas.Length} real formulas parsed clean");
        Assert.Equal(ValidFormulas.Length, ok);
    }
}
