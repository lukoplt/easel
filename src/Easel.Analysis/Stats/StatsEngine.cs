using Easel.Core;
using Easel.Core.Symbols;
using Easel.Fx;

namespace Easel.Analysis.Stats;

public sealed record ScreenStat(string Name, int ControlCount, int MaxControlDepth);

public sealed record AppStats(
    int ScreenCount,
    int ControlCount,
    int ComponentCount,
    int DataSourceCount,
    int MediaCount,
    long MediaBytes,
    int GlobalVariableCount,
    int ContextVariableCount,
    int CollectionCount,
    int NamedFormulaCount,
    int FormulaCount,
    int TotalFormulaNodes,
    double AverageFormulaComplexity,
    int MaxFormulaComplexity,
    string? MostComplexFormulaPath,
    IReadOnlyList<ScreenStat> Screens);

/// <summary>Computes app metrics from the shared analysis (controls, media, complexity).</summary>
public static class StatsEngine
{
    public static AppStats Compute(AppAnalysis a)
    {
        var model = a.Model;

        var screens = model.Screens
            .Select(s => new ScreenStat(s.Name, s.AllControls().Count(), MaxDepth(s.Children)))
            .OrderByDescending(s => s.ControlCount)
            .ToList();

        int totalNodes = 0, formulaCount = 0, maxComplexity = 0;
        string? maxPath = null;
        foreach (var pr in model.AllProperties().Where(p => p.Property.HasFormula))
        {
            var parse = a.Fx.Parse(pr.Property.Formula);
            if (parse is not { IsSuccess: true, Root: not null }) continue;
            var nodes = AstMetrics.NodeCount(parse.Root);
            formulaCount++;
            totalNodes += nodes;
            if (nodes > maxComplexity) { maxComplexity = nodes; maxPath = pr.Path; }
        }

        return new AppStats(
            ScreenCount: model.Screens.Count,
            ControlCount: model.AllControls().Count(),
            ComponentCount: model.Components.Count,
            DataSourceCount: model.DataSources.Count,
            MediaCount: model.Media.Count,
            MediaBytes: model.Media.Sum(m => m.SizeBytes ?? 0),
            GlobalVariableCount: a.Symbols.OfKind(SymbolKind.GlobalVariable).Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            ContextVariableCount: a.Symbols.OfKind(SymbolKind.ContextVariable).Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            CollectionCount: a.Symbols.OfKind(SymbolKind.Collection).Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            NamedFormulaCount: model.App.Formulas.Count,
            FormulaCount: formulaCount,
            TotalFormulaNodes: totalNodes,
            AverageFormulaComplexity: formulaCount == 0 ? 0 : Math.Round((double)totalNodes / formulaCount, 1),
            MaxFormulaComplexity: maxComplexity,
            MostComplexFormulaPath: maxPath,
            Screens: screens);
    }

    private static int MaxDepth(IReadOnlyList<Core.Model.Control> controls, int depth = 1)
    {
        int max = controls.Count == 0 ? 0 : depth;
        foreach (var c in controls)
            if (c.Children.Count > 0)
                max = Math.Max(max, MaxDepth(c.Children, depth + 1));
        return max;
    }
}
