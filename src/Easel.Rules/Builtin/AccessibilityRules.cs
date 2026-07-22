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

/// <summary>Shared literal helpers for the accessibility rules.</summary>
internal static class A11y
{
    /// <summary>A property whose formula is the literal <c>true</c>.</summary>
    public static bool IsTrue(Property? p) =>
        p is { HasFormula: true } && string.Equals(p.Formula.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>A property whose formula is the literal <c>false</c>.</summary>
    public static bool IsFalse(Property? p) =>
        p is { HasFormula: true } && string.Equals(p.Formula.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>Numeric literal value of a property formula, or null when not a plain number.</summary>
    public static decimal? AsNumber(Property? p) =>
        p is { HasFormula: true } &&
        decimal.TryParse(p.Formula.Trim(), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n : null;

    /// <summary>An empty string literal or Blank().</summary>
    public static bool IsEmptyText(string formula)
    {
        var f = formula.Trim();
        return f is "" or "\"\"" or "''"
            || string.Equals(f, "Blank()", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>PA1020 — FocusedBorderThickness of 0 makes keyboard focus invisible.</summary>
public sealed class FocusNotVisibleRule : RuleBase
{
    public override string Id => "PA1020";
    public override string Name => "focus-not-visible";
    public override RuleCategory Category => RuleCategory.Accessibility;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var screen in ctx.Model.Screens)
            foreach (var c in screen.AllControls())
                if (A11y.AsNumber(c.GetProperty("FocusedBorderThickness")) == 0)
                    yield return Report(
                        $"Control '{c.Name}' has FocusedBorderThickness 0 — keyboard focus is invisible.",
                        c.Location, $"{screen.Name}/{c.Name}",
                        help: "Set FocusedBorderThickness above 0 so keyboard users can see where focus is.");
    }
}

/// <summary>PA1021 — an Audio/Video control without closed captions.</summary>
public sealed class MissingCaptionsRule : RuleBase
{
    public override string Id => "PA1021";
    public override string Name => "missing-captions";
    public override RuleCategory Category => RuleCategory.Accessibility;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var screen in ctx.Model.Screens)
        {
            foreach (var c in screen.AllControls())
            {
                if (BaseType(c) is not ("Audio" or "Video")) continue;
                var url = c.GetProperty("ClosedCaptionsUrl");
                if (url is { HasFormula: true } && !A11y.IsEmptyText(url.Formula)) continue;

                yield return Report(
                    $"{BaseType(c)} control '{c.Name}' has no ClosedCaptionsUrl.",
                    c.Location, $"{screen.Name}/{c.Name}",
                    help: "Provide WebVTT captions so users with hearing impairments get the content.");
            }
        }
    }
}

/// <summary>PA1022 — a screen still has its default name (Screen1, Screen2, …).</summary>
public sealed class DefaultScreenNameRule : RuleBase
{
    public override string Id => "PA1022";
    public override string Name => "default-screen-name";
    public override RuleCategory Category => RuleCategory.Accessibility;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var screen in ctx.Model.Screens)
        {
            if (!IsDefaultName(screen.Name)) continue;
            yield return Report(
                $"Screen '{screen.Name}' has a default name.",
                screen.Location, screen.Name,
                help: "Screen readers announce screen names on navigation — use a name that describes the screen's purpose.");
        }
    }

    private static bool IsDefaultName(string name) =>
        name.StartsWith("Screen", StringComparison.OrdinalIgnoreCase)
        && name["Screen".Length..].All(char.IsDigit);
}

/// <summary>PA1023 — a TabIndex greater than 0 (custom tab orders break screen readers).</summary>
public sealed class PositiveTabIndexRule : RuleBase
{
    public override string Id => "PA1023";
    public override string Name => "positive-tab-index";
    public override RuleCategory Category => RuleCategory.Accessibility;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var screen in ctx.Model.Screens)
            foreach (var c in screen.AllControls())
                if (A11y.AsNumber(c.GetProperty("TabIndex")) is > 0 and var ti)
                    yield return Report(
                        $"Control '{c.Name}' has TabIndex {ti} — custom tab orders are hard to maintain and break screen readers.",
                        c.Location, $"{screen.Name}/{c.Name}",
                        help: "Use TabIndex 0 (focusable, natural order) or -1; reorder with layout/containers instead.");
    }
}

/// <summary>PA1024 — an Audio/Video control that starts playing automatically.</summary>
public sealed class AutostartMediaRule : RuleBase
{
    public override string Id => "PA1024";
    public override string Name => "autostart-media";
    public override RuleCategory Category => RuleCategory.Accessibility;
    public override Severity DefaultSeverity => Severity.Warning;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var screen in ctx.Model.Screens)
            foreach (var c in screen.AllControls())
                if (BaseType(c) is "Audio" or "Video" && A11y.IsTrue(c.GetProperty("AutoStart")))
                    yield return Report(
                        $"{BaseType(c)} control '{c.Name}' autostarts.",
                        c.Location, $"{screen.Name}/{c.Name}",
                        help: "Autoplaying media is disorienting and hard to stop for keyboard users — let the user start playback.");
    }
}

/// <summary>PA1025 — a stateful control (toggle, slider, rating) hides its value.</summary>
public sealed class StateIndicationRule : RuleBase
{
    public override string Id => "PA1025";
    public override string Name => "state-indication";
    public override RuleCategory Category => RuleCategory.Accessibility;
    public override Severity DefaultSeverity => Severity.Info;

    private static readonly HashSet<string> Stateful = new(StringComparer.OrdinalIgnoreCase)
        { "Toggle", "Slider", "Rating", "Checkbox" };

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var screen in ctx.Model.Screens)
            foreach (var c in screen.AllControls())
                if (Stateful.Contains(BaseType(c)) && A11y.IsFalse(c.GetProperty("ShowValue")))
                    yield return Report(
                        $"{BaseType(c)} '{c.Name}' has ShowValue false — its state is not announced.",
                        c.Location, $"{screen.Name}/{c.Name}",
                        help: "Set ShowValue to true so users get confirmation of the control's current state.");
    }
}

/// <summary>
/// PA1030 — text colour vs fill contrast below WCAG AA (4.5:1). Checked only when both
/// Color and Fill are opaque RGBA literals on the same control, so it never guesses at
/// inherited or computed colours.
/// </summary>
public sealed class LowContrastRule : RuleBase
{
    public override string Id => "PA1030";
    public override string Name => "low-contrast";
    public override RuleCategory Category => RuleCategory.Accessibility;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var minRatio = ctx.Options.Child("min-ratio").AsDouble() ?? 4.5;

        foreach (var screen in ctx.Model.Screens)
        {
            foreach (var c in screen.AllControls())
            {
                var fg = ParseOpaqueRgba(c.GetProperty("Color")?.Formula);
                var bg = ParseOpaqueRgba(c.GetProperty("Fill")?.Formula);
                if (fg is null || bg is null) continue;

                var ratio = ContrastRatio(fg.Value, bg.Value);
                if (ratio >= minRatio) continue;

                yield return Report(
                    $"Control '{c.Name}' text contrast is {ratio:0.0}:1 (minimum {minRatio}:1).",
                    c.Location, $"{screen.Name}/{c.Name}",
                    help: "Users with low vision cannot read low-contrast text. Darken the text or lighten the fill (WCAG AA needs 4.5:1).");
            }
        }
    }

    /// <summary>Parses an <c>RGBA(r, g, b, 1)</c> literal; null for anything else (incl. translucency).</summary>
    public static (int R, int G, int B)? ParseOpaqueRgba(string? formula)
    {
        if (formula is null) return null;
        var f = formula.Trim();
        if (!f.StartsWith("RGBA(", StringComparison.OrdinalIgnoreCase) || !f.EndsWith(')')) return null;
        var parts = f["RGBA(".Length..^1].Split(',');
        if (parts.Length != 4) return null;
        if (!int.TryParse(parts[0].Trim(), out var r) || !int.TryParse(parts[1].Trim(), out var g)
            || !int.TryParse(parts[2].Trim(), out var b)) return null;
        if (!double.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var a) || a < 1) return null;
        return (r, g, b);
    }

    /// <summary>WCAG relative-luminance contrast ratio, always ≥ 1.</summary>
    public static double ContrastRatio((int R, int G, int B) c1, (int R, int G, int B) c2)
    {
        var l1 = Luminance(c1);
        var l2 = Luminance(c2);
        var (hi, lo) = l1 >= l2 ? (l1, l2) : (l2, l1);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance((int R, int G, int B) c)
    {
        static double Chan(int v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Chan(c.R) + 0.7152 * Chan(c.G) + 0.0722 * Chan(c.B);
    }
}

/// <summary>PA1026 — a Pen input with no alternative text input on the same screen.</summary>
public sealed class PenAlternativeInputRule : RuleBase
{
    public override string Id => "PA1026";
    public override string Name => "pen-alternative-input";
    public override RuleCategory Category => RuleCategory.Accessibility;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var screen in ctx.Model.Screens)
        {
            var controls = screen.AllControls().ToList();
            var pens = controls.Where(c => BaseType(c) is "PenInput").ToList();
            if (pens.Count == 0 || controls.Any(c => BaseType(c) is "TextInput")) continue;

            foreach (var pen in pens)
                yield return Report(
                    $"Pen control '{pen.Name}' has no alternative input method on screen '{screen.Name}'.",
                    pen.Location, $"{screen.Name}/{pen.Name}",
                    help: "Add a Text input next to the Pen control for users who cannot use a pen.");
        }
    }
}
