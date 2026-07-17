using System.Text;
using PaCheck.Core;
using PaCheck.Core.Graph;
using Xunit;
using static VerifyXunit.Verifier;

namespace PaCheck.Tests;

/// <summary>
/// Snapshot tests (T1.6) over the fixture: model outline, symbol table and dependency
/// graph. A change in loader/symbol/graph behaviour shows up as a reviewable diff.
/// </summary>
public sealed class SnapshotTests
{
    [Fact]
    public Task Model_symbols_and_graph_snapshot()
    {
        var a = AppAnalysis.FromFolder(TestPaths.SampleApp);
        var sb = new StringBuilder();

        sb.AppendLine("# MODEL");
        sb.AppendLine($"app formulas: {string.Join(", ", a.Model.App.Formulas.Select(f => f.Name))}");
        foreach (var s in a.Model.Screens.OrderBy(s => s.Name))
        {
            sb.AppendLine($"screen {s.Name}");
            foreach (var c in s.AllControls().OrderBy(c => c.Name))
                sb.AppendLine($"  {c.Name}: {c.ControlType}@{c.Version}");
        }

        sb.AppendLine("\n# SYMBOLS");
        foreach (var d in a.Symbols.Definitions.OrderBy(d => d.Kind).ThenBy(d => d.Name))
            sb.AppendLine($"{d.Kind} {d.Name} reads={a.Symbols.ReadCount(d.Name)}");

        sb.AppendLine("\n# GRAPH EDGES");
        foreach (var e in a.Graph.Edges.Where(e => e.Kind == EdgeKind.NavigatesTo)
                     .OrderBy(e => e.From).ThenBy(e => e.To))
            sb.AppendLine($"{e.From} --{e.Kind}--> {e.To}");

        return Verify(sb.ToString());
    }
}
