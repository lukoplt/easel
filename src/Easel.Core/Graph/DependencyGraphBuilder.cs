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

        // Context variables are screen-scoped: two screens may each have their own 'locX',
        // so their node identity includes the scope. Everything else is app-wide (canvas
        // enforces unique control names).
        static string NodeId(SymbolKind kind, string name, string? scope) =>
            kind == SymbolKind.ContextVariable ? $"ContextVariable:{scope}:{name}" : $"{kind}:{name}";

        foreach (var d in symbols.Definitions)
            nodes.Add(new GraphNode(NodeId(d.Kind, d.Name, d.Scope), d.Kind.ToString(), d.Name));

        nodes.Add(new GraphNode("App", "App", "App"));

        // Resolve a referenced name to its symbol node id, preferring a context variable in
        // the reading scope, else the first matching app symbol.
        string? TargetId(string name, string? scope)
        {
            var defs = symbols.DefinitionsOf(name);
            if (defs.Count == 0) return null;
            var def = defs.FirstOrDefault(d => d.Kind == SymbolKind.ContextVariable
                          && string.Equals(d.Scope, scope, StringComparison.OrdinalIgnoreCase))
                      ?? defs[0];
            return NodeId(def.Kind, def.Name, def.Scope);
        }

        foreach (var pr in model.AllProperties())
        {
            if (!pr.Property.HasFormula) continue;
            var ownerId = OwnerId(pr);
            var scope = ScopeOf(pr);
            var facts = fx.Facts(pr.Property.Formula);

            foreach (var n in facts.FirstNames)
            {
                var target = TargetId(n.Name, scope);
                if (target is not null && target != ownerId)
                    edges.Add(new GraphEdge(ownerId, target, EdgeKind.Reads));
            }

            // Navigation edges: Navigate(screen, ...) / Back().
            foreach (var call in facts.Calls.Where(c => c.Is("Navigate")))
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

    /// <summary>Screen (or component) scope a property is evaluated in; null for App.</summary>
    private static string? ScopeOf(PropertyRef pr) => pr.OwnerKind switch
    {
        OwnerKind.Screen => pr.OwnerName,
        OwnerKind.Control => pr.ScreenName,
        OwnerKind.Component => pr.OwnerName,
        _ => null,
    };
}
