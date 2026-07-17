using Easel.Core;
using Easel.Core.Symbols;
using Xunit;

namespace Easel.Tests;

public sealed class SymbolTableTests
{
    private static AppAnalysis Analyze() => AppAnalysis.FromFolder(TestPaths.SampleApp);

    [Fact]
    public void Detects_global_variable_definitions()
    {
        var a = Analyze();
        var globals = a.Symbols.OfKind(SymbolKind.GlobalVariable).Select(d => d.Name).ToHashSet();
        Assert.Contains("gblUser", globals);
        Assert.Contains("gblUnused", globals);
        Assert.Contains("gblTheme", globals);
    }

    [Fact]
    public void Detects_collection_definitions()
    {
        var a = Analyze();
        var colls = a.Symbols.OfKind(SymbolKind.Collection).Select(d => d.Name).ToHashSet();
        Assert.Contains("colItems", colls);
        Assert.Contains("colOrphan", colls);
    }

    [Fact]
    public void Unused_variable_has_zero_reads()
    {
        var a = Analyze();
        Assert.Equal(0, a.Symbols.ReadCount("gblUnused"));
        Assert.Equal(0, a.Symbols.ReadCount("colOrphan"));
    }

    [Fact]
    public void Used_variable_has_reads()
    {
        var a = Analyze();
        Assert.True(a.Symbols.ReadCount("gblUser") > 0);
        Assert.True(a.Symbols.ReadCount("colItems") > 0);
        Assert.True(a.Symbols.ReadCount("varLocal") > 0);
        Assert.True(a.Symbols.ReadCount("gblTheme") > 0);
    }

    [Fact]
    public void Navigation_edge_recorded_from_home_to_detail()
    {
        var a = Analyze();
        var navs = a.Graph.Edges.Where(e => e.Kind == Core.Graph.EdgeKind.NavigatesTo).ToList();
        Assert.Contains(navs, e => e.From == "Screen:scrHome" && e.To == "Screen:scrDetail");
    }
}
