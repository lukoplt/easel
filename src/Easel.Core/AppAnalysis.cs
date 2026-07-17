using Easel.Core.Graph;
using Easel.Core.Loader;
using Easel.Core.Model;
using Easel.Core.Symbols;
using Easel.Fx;

namespace Easel.Core;

/// <summary>
/// The shared, build-once analysis context: model, parse service, symbol table and
/// dependency graph. Every command consumes this so nothing is parsed twice.
/// </summary>
public sealed class AppAnalysis
{
    public AppModel Model { get; }
    public FxParseService Fx { get; }
    public SymbolTable Symbols { get; }
    public DependencyGraph Graph { get; }
    public IReadOnlyList<LoadDiagnostic> LoadDiagnostics { get; }

    private AppAnalysis(AppModel model, FxParseService fx, SymbolTable symbols,
        DependencyGraph graph, IReadOnlyList<LoadDiagnostic> diags)
    {
        Model = model;
        Fx = fx;
        Symbols = symbols;
        Graph = graph;
        LoadDiagnostics = diags;
    }

    /// <summary>Load an unpacked app folder and build the full analysis context.</summary>
    public static AppAnalysis FromFolder(string root)
    {
        var load = YamlLoader.LoadFolder(root);
        var model = AppModelBuilder.Build(load);
        return FromModel(model, load.Diagnostics);
    }

    /// <summary>
    /// Load-level problems (a whole source file that failed to parse) surfaced as findings,
    /// so a broken file is never silently dropped from analysis.
    /// </summary>
    public IEnumerable<Finding> LoadFindings() =>
        LoadDiagnostics.Select(d => new Finding(
            "PA0002", "file-load-error", RuleCategory.Error, Severity.Error,
            $"Could not load source file: {d.Message}",
            new SourceLocation(d.RelativePath, d.Line, d.Column), d.RelativePath,
            "Fix the YAML syntax, or re-open and re-save the app in Power Apps Studio."));

    public static AppAnalysis FromModel(AppModel model, IReadOnlyList<LoadDiagnostic>? diags = null)
    {
        var fx = new FxParseService();
        var symbols = SymbolTableBuilder.Build(model, fx);
        var graph = DependencyGraphBuilder.Build(model, symbols, fx);
        return new AppAnalysis(model, fx, symbols, graph, diags ?? Array.Empty<LoadDiagnostic>());
    }
}
