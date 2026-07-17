using PaCheck.Core;
using PaCheck.Core.Config;
using PaCheck.Core.Graph;
using PaCheck.Core.Model;
using PaCheck.Core.Symbols;
using PaCheck.Fx;

namespace PaCheck.Rules;

/// <summary>A lint rule. Stateless: discovered by reflection and evaluated per run.</summary>
public interface IRule
{
    string Id { get; }              // e.g. "PA1003"
    string Name { get; }            // e.g. "unused-variable"
    RuleCategory Category { get; }
    Severity DefaultSeverity { get; }

    IEnumerable<Finding> Evaluate(RuleContext ctx);
}

/// <summary>Everything a rule needs: the model, symbols, graph, parser and its own config.</summary>
public sealed class RuleContext
{
    public AppModel Model { get; }
    public SymbolTable Symbols { get; }
    public DependencyGraph Graph { get; }
    public FxParseService Fx { get; }
    public ConfigNode Options { get; }

    public RuleContext(AppAnalysis analysis, ConfigNode options)
    {
        Model = analysis.Model;
        Symbols = analysis.Symbols;
        Graph = analysis.Graph;
        Fx = analysis.Fx;
        Options = options;
    }

    /// <summary>All properties in the app that hold a formula.</summary>
    public IEnumerable<PropertyRef> Formulas() =>
        Model.AllProperties().Where(p => p.Property.HasFormula);
}
