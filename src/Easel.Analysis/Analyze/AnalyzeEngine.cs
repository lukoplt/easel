using Easel.Core;
using Easel.Core.Graph;
using Easel.Core.Symbols;

namespace Easel.Analysis.Analyze;

public sealed record SymbolUsage(
    string Name,
    IReadOnlyList<SymbolDefinition> Definitions,
    IReadOnlyList<SymbolReference> Usages);

public sealed record DeadCodeReport(
    IReadOnlyList<SymbolDefinition> UnusedVariables,
    IReadOnlyList<SymbolDefinition> UnusedCollections,
    IReadOnlyList<Core.Model.MediaAsset> UnusedMedia,
    IReadOnlyList<string> UnreachableScreens)
{
    public int Total => UnusedVariables.Count + UnusedCollections.Count + UnusedMedia.Count + UnreachableScreens.Count;
}

/// <summary>find-usages / dead-code / impact / graph queries over the dependency graph.</summary>
public static class AnalyzeEngine
{
    public static SymbolUsage Find(AppAnalysis a, string name) =>
        new(name, a.Symbols.DefinitionsOf(name), a.Symbols.Usages(name));

    public static DeadCodeReport DeadCode(AppAnalysis a)
    {
        var unusedVars = a.Symbols.OfKind(SymbolKind.GlobalVariable)
            .Concat(a.Symbols.OfKind(SymbolKind.ContextVariable))
            .Where(d => a.Symbols.ReadCount(d.Name) == 0)
            .DistinctBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unusedColls = a.Symbols.OfKind(SymbolKind.Collection)
            .Where(d => a.Symbols.ReadCount(d.Name) == 0)
            .DistinctBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Media: unused if not referenced by identifier nor mentioned in any string literal.
        var literals = a.Model.AllProperties()
            .Where(p => p.Property.HasFormula)
            .SelectMany(p => a.Fx.Facts(p.Property.Formula).Strings.Select(s => s.Value))
            .ToList();
        var unusedMedia = a.Model.Media
            .Where(m => a.Symbols.ReadCount(m.Name) == 0
                && !literals.Any(l => l.Contains(m.Name, StringComparison.OrdinalIgnoreCase)
                    || (m.FileName is not null && l.Contains(m.FileName, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var unreachable = FindOrphanScreens(a);
        return new DeadCodeReport(unusedVars, unusedColls, unusedMedia, unreachable);
    }

    /// <summary>
    /// Screens that are truly orphaned: no navigation in or out. Deliberately conservative —
    /// the app start screen has only outgoing navigation, so requiring "no incoming edge"
    /// alone would flag it. Returns nothing when the app has no navigation at all (unknowable).
    /// </summary>
    private static IReadOnlyList<string> FindOrphanScreens(AppAnalysis a)
    {
        var navEdges = a.Graph.Edges.Where(e => e.Kind == EdgeKind.NavigatesTo).ToList();
        if (navEdges.Count == 0 || a.Model.Screens.Count <= 1) return Array.Empty<string>();

        var startScreen = ResolveStartScreen(a);

        return a.Model.Screens
            .Where(s => !string.Equals(s.Name, startScreen, StringComparison.OrdinalIgnoreCase))
            .Where(s =>
            {
                var id = $"{SymbolKind.Screen}:{s.Name}";
                var hasIncoming = a.Graph.IncomingTo(id).Any(e => e.Kind == EdgeKind.NavigatesTo);
                var hasOutgoing = a.Graph.OutgoingFrom(id).Any(e => e.Kind == EdgeKind.NavigatesTo);
                return !hasIncoming && !hasOutgoing;
            })
            .Select(s => s.Name)
            .ToList();
    }

    private static string? ResolveStartScreen(AppAnalysis a)
    {
        var start = a.Model.App.GetProperty("StartScreen");
        if (start is not { HasFormula: true }) return null;
        return a.Fx.Facts(start.Formula).FirstNames
            .Select(n => n.Name)
            .FirstOrDefault(n => a.Symbols.DefinitionsOf(n).Any(d => d.Kind == SymbolKind.Screen));
    }

    public static IReadOnlyList<GraphNode> Impact(AppAnalysis a, string name)
    {
        var def = a.Symbols.DefinitionsOf(name).FirstOrDefault();
        if (def is null) return Array.Empty<GraphNode>();
        return a.Graph.ImpactOf($"{def.Kind}:{def.Name}");
    }

    public static string Graph(AppAnalysis a, string format) =>
        format.Equals("dot", StringComparison.OrdinalIgnoreCase) ? a.Graph.ToDot() : a.Graph.ToMermaid();
}
