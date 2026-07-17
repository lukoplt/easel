using Easel.Core.Model;
using Easel.Core.Symbols;
using Easel.Fx;

namespace Easel.Core.Graph;

/// <summary>Builds the <see cref="DependencyGraph"/> from the model, symbol table and facts.</summary>
public static class DependencyGraphBuilder
{
    public static DependencyGraph Build(AppModel model, SymbolTable symbols, FxParseService fx)
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();

        GraphNode SymbolNode(SymbolDefinition d) => new($"{d.Kind}:{d.Name}", d.Kind.ToString(), d.Name);
        foreach (var d in symbols.Definitions)
            nodes.Add(SymbolNode(d));

        nodes.Add(new GraphNode("App", "App", "App"));

        // Resolve a referenced name to its symbol node id, if it is a real app symbol.
        string? TargetId(string name)
        {
            var def = symbols.DefinitionsOf(name).FirstOrDefault();
            return def is null ? null : $"{def.Kind}:{def.Name}";
        }

        foreach (var pr in model.AllProperties())
        {
            if (!pr.Property.HasFormula) continue;
            var ownerId = OwnerId(pr);
            var facts = fx.Facts(pr.Property.Formula);

            foreach (var n in facts.FirstNames)
            {
                var target = TargetId(n.Name);
                if (target is not null && target != ownerId)
                    edges.Add(new GraphEdge(ownerId, target, EdgeKind.Reads));
            }

            // Navigation edges: Navigate(screen, ...) / Back().
            foreach (var call in facts.Calls.Where(c => c.Name is "Navigate"))
            {
                if (call.Args.Count > 0 && call.Args[0] is Microsoft.PowerFx.Syntax.FirstNameNode fn)
                {
                    var toScreen = fn.Ident.Name.Value;
                    if (symbols.DefinitionsOf(toScreen).Any(d => d.Kind == SymbolKind.Screen))
                        edges.Add(new GraphEdge(ScreenIdFor(pr), $"{SymbolKind.Screen}:{toScreen}", EdgeKind.NavigatesTo));
                }
            }
        }

        return new DependencyGraph(nodes, edges);
    }

    private static string OwnerId(PropertyRef pr) => pr.OwnerKind switch
    {
        OwnerKind.Screen => $"{SymbolKind.Screen}:{pr.OwnerName}",
        OwnerKind.Control => $"{SymbolKind.Control}:{pr.OwnerName}",
        _ => "App",
    };

    private static string ScreenIdFor(PropertyRef pr) =>
        pr.ScreenName is { Length: > 0 } s ? $"{SymbolKind.Screen}:{s}"
        : pr.OwnerKind == OwnerKind.Screen ? $"{SymbolKind.Screen}:{pr.OwnerName}"
        : "App";
}
