using Easel.Core.Model;

namespace Easel.Rules.Builtin;

/// <summary>PA1009 — an interactive control has no non-empty AccessibleLabel.</summary>
public sealed class MissingAccessibleLabelRule : RuleBase
{
    public override string Id => "PA1009";
    public override string Name => "missing-accessible-label";
    public override RuleCategory Category => RuleCategory.Accessibility;
    public override Severity DefaultSeverity => Severity.Warning;

    private static readonly HashSet<string> Interactive = new(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "Toggle", "Slider", "TextInput", "Checkbox", "Radio", "DropDown",
        "Dropdown", "ComboBox", "Combobox", "DatePicker", "Rating", "ListBox", "PenInput",
    };

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var screen in ctx.Model.Screens)
        {
            foreach (var c in screen.AllControls())
            {
                if (!IsInteractive(c)) continue;
                var label = c.GetProperty("AccessibleLabel");
                if (label is { HasFormula: true } && !IsEmptyLabel(label.Formula)) continue;

                yield return Report(
                    $"Interactive control '{c.Name}' ({BaseType(c)}) has no AccessibleLabel.",
                    c.Location, $"{screen.Name}/{c.Name}",
                    help: "Set AccessibleLabel so screen readers can announce this control.");
            }
        }
    }

    private static bool IsInteractive(Control c) =>
        Interactive.Contains(BaseType(c)) || c.GetProperty("OnSelect") is { HasFormula: true };

    /// <summary>An empty string or Blank() is not a usable accessible label.</summary>
    private static bool IsEmptyLabel(string formula)
    {
        var f = formula.Trim();
        return f is "" or "\"\"" or "''"
            || string.Equals(f, "Blank()", StringComparison.OrdinalIgnoreCase);
    }
}
