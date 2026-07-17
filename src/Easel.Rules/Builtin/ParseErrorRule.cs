using Easel.Core.Model;

namespace Easel.Rules.Builtin;

/// <summary>PF0001 — a formula could not be parsed by Power Fx.</summary>
public sealed class ParseErrorRule : RuleBase
{
    public override string Id => "PF0001";
    public override string Name => "unparsable-formula";
    public override RuleCategory Category => RuleCategory.Error;
    public override Severity DefaultSeverity => Severity.Error;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        foreach (var pr in ctx.Formulas())
        {
            var parse = ctx.Fx.Parse(pr.Property.Formula);
            if (parse.IsSuccess) continue;

            var detail = parse.Errors.Count > 0 ? parse.Errors[0].Message : "unknown parse error";
            yield return Report(
                $"Could not parse formula: {detail}",
                pr.Property.Location, pr.Path,
                help: "Fix the syntax, or re-open and re-save the app in Power Apps Studio if it predates the YAML format.");
        }
    }
}
