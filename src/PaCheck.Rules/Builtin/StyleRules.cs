using System.Text.RegularExpressions;
using PaCheck.Core.Model;
using PaCheck.Fx;

namespace PaCheck.Rules.Builtin;

/// <summary>PA1008 — a colour property uses a hardcoded literal instead of a theme value.</summary>
public sealed partial class HardcodedColorRule : RuleBase
{
    public override string Id => "PA1008";
    public override string Name => "hardcoded-color";
    public override RuleCategory Category => RuleCategory.Maintainability;
    public override Severity DefaultSeverity => Severity.Info;

    private static readonly HashSet<string> ColorProps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fill", "Color", "BorderColor", "HoverFill", "HoverColor", "HoverBorderColor",
        "PressedFill", "PressedColor", "PressedBorderColor", "FocusedBorderColor",
        "DisabledFill", "DisabledColor", "DisabledBorderColor", "SelectionColor",
        "ChevronFill", "ChevronBackground",
    };

    [GeneratedRegex(@"RGBA\s*\(\s*\d|ColorValue\s*\(\s*""#|Color\.[A-Za-z]", RegexOptions.IgnoreCase)]
    private static partial Regex ColorLiteral();

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            if (!ColorProps.Contains(pr.Property.Name)) continue;
            if (!ColorLiteral().IsMatch(pr.Property.Formula)) continue;

            yield return Report(
                $"{pr.Property.Name} uses a hardcoded colour literal.",
                pr.Property.Location, pr.Path,
                help: "Reference a theme/global colour so palette changes stay in one place.");
        }
    }
}

/// <summary>PA1010 — deeply nested If, better expressed as Switch.</summary>
public sealed class DeepNestedIfRule : RuleBase
{
    public override string Id => "PA1010";
    public override string Name => "deep-nested-if";
    public override RuleCategory Category => RuleCategory.Maintainability;
    public override Severity DefaultSeverity => Severity.Info;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var threshold = ctx.Options.Child("threshold").AsInt() ?? 2;

        foreach (var pr in ctx.Formulas())
        {
            var parse = ctx.Fx.Parse(pr.Property.Formula);
            if (parse is not { IsSuccess: true, Root: not null }) continue;

            var depth = AstMetrics.MaxCallNesting(parse.Root, "If");
            if (depth > threshold)
                yield return Report(
                    $"'If' nested {depth} deep (threshold {threshold}).",
                    pr.Property.Location, pr.Path,
                    help: "Flatten with Switch(...) or intermediate variables.");
        }
    }
}

/// <summary>PA1011 — a Timer whose handler hides side-effects (periodic writes/navigation).</summary>
public sealed class TimerSideEffectRule : RuleBase
{
    public override string Id => "PA1011";
    public override string Name => "timer-side-effect";
    public override RuleCategory Category => RuleCategory.Maintainability;
    public override Severity DefaultSeverity => Severity.Info;

    private static readonly string[] Handlers = { "OnTimerEnd", "OnTimerStart" };

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var screen in ctx.Model.Screens)
        {
            foreach (var c in screen.AllControls())
            {
                if (!string.Equals(BaseType(c), "Timer", StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var handler in Handlers)
                {
                    var p = c.GetProperty(handler);
                    if (p is not { HasFormula: true }) continue;
                    var facts = ctx.Fx.Facts(p.Formula);
                    var sideEffects = facts.Writes.Count +
                        facts.Calls.Count(x => x.Name is "Navigate" or "Patch" or "Remove" or "RemoveIf");
                    if (sideEffects > 0)
                        yield return Report(
                            $"Timer '{c.Name}'.{handler} performs {sideEffects} side-effect(s) on a timer.",
                            p.Location, $"{screen.Name}/{c.Name}",
                            help: "Hidden periodic side-effects are hard to reason about; prefer explicit user actions.");
                }
            }
        }
    }
}
