using PaCheck.Core.Model;

namespace PaCheck.Rules;

/// <summary>Convenience base: declares identity once and offers a finding factory.</summary>
public abstract class RuleBase : IRule
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract RuleCategory Category { get; }
    public abstract Severity DefaultSeverity { get; }

    public abstract IEnumerable<Finding> Evaluate(RuleContext ctx);

    protected Finding Report(string message, SourceLocation loc, string? elementPath = null, string? help = null) =>
        new(Id, Name, Category, DefaultSeverity, message, loc, elementPath, help);

    /// <summary>Base control type without the "Classic/" or other namespace prefix.</summary>
    protected static string BaseType(Control c)
    {
        var t = c.ControlType;
        var slash = t.LastIndexOf('/');
        return slash >= 0 ? t[(slash + 1)..] : t;
    }
}
