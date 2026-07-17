using System.Text;

namespace PaCheck.Core.Graph;

public enum EdgeKind { Reads, NavigatesTo, Defines, BindsTo }

public sealed record GraphNode(string Id, string Kind, string Name);
public sealed record GraphEdge(string From, string To, EdgeKind Kind);

/// <summary>
/// Directed graph of app elements and symbols. Answers reachability questions
/// (dead-code, impact) and exports to Mermaid / Graphviz DOT.
/// </summary>
public sealed class DependencyGraph
{
    private readonly Dictionary<string, GraphNode> _nodes;
    private readonly List<GraphEdge> _edges;
    private readonly ILookup<string, GraphEdge> _out;
    private readonly ILookup<string, GraphEdge> _in;

    public IReadOnlyCollection<GraphNode> Nodes => _nodes.Values;
    public IReadOnlyList<GraphEdge> Edges => _edges;

    public DependencyGraph(IEnumerable<GraphNode> nodes, IEnumerable<GraphEdge> edges)
    {
        _nodes = nodes.GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        _edges = edges.Distinct().ToList();
        _out = _edges.ToLookup(e => e.From);
        _in = _edges.ToLookup(e => e.To);
    }

    public GraphNode? Node(string id) => _nodes.GetValueOrDefault(id);

    public IEnumerable<GraphEdge> OutgoingFrom(string id) => _out[id];
    public IEnumerable<GraphEdge> IncomingTo(string id) => _in[id];

    /// <summary>Nodes of the given kinds that have no incoming edge of the given kind.</summary>
    public IEnumerable<GraphNode> NodesWithNoIncoming(EdgeKind edge, params string[] kinds)
    {
        var set = new HashSet<string>(kinds, StringComparer.OrdinalIgnoreCase);
        foreach (var n in _nodes.Values)
        {
            if (kinds.Length > 0 && !set.Contains(n.Kind)) continue;
            if (!_in[n.Id].Any(e => e.Kind == edge))
                yield return n;
        }
    }

    /// <summary>Transitive set of nodes reachable backwards from <paramref name="id"/> (impact).</summary>
    public IReadOnlyList<GraphNode> ImpactOf(string id, EdgeKind via = EdgeKind.Reads)
    {
        var seen = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var e in _in[cur].Where(e => e.Kind == via))
            {
                if (seen.Add(e.From))
                    queue.Enqueue(e.From);
            }
        }
        return seen.Select(s => _nodes.GetValueOrDefault(s)).Where(n => n is not null).Cast<GraphNode>().ToList();
    }

    public string ToMermaid()
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph TD");
        var ids = _nodes.Values.ToDictionary(n => n.Id, n => "n" + Math.Abs(n.Id.GetHashCode()));
        foreach (var n in _nodes.Values.OrderBy(n => n.Id))
            sb.AppendLine($"  {ids[n.Id]}[\"{n.Kind}: {n.Name}\"]");
        foreach (var e in _edges)
        {
            if (!ids.TryGetValue(e.From, out var f) || !ids.TryGetValue(e.To, out var t)) continue;
            var arrow = e.Kind == EdgeKind.NavigatesTo ? "-->|nav|" : "-->";
            sb.AppendLine($"  {f} {arrow} {t}");
        }
        return sb.ToString();
    }

    public string ToDot()
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph app {");
        sb.AppendLine("  rankdir=LR;");
        foreach (var n in _nodes.Values.OrderBy(n => n.Id))
            sb.AppendLine($"  \"{n.Id}\" [label=\"{n.Kind}: {n.Name}\"];");
        foreach (var e in _edges)
            sb.AppendLine($"  \"{e.From}\" -> \"{e.To}\" [label=\"{e.Kind}\"];");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
