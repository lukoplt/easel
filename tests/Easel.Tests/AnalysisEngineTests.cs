using Easel.Analysis.Analyze;
using Easel.Analysis.Diff;
using Easel.Analysis.Stats;
using AppDiffEngine = Easel.Analysis.Diff.DiffEngine;
using Easel.Core;
using Easel.Core.Loader;
using Xunit;

namespace Easel.Tests;

public sealed class AnalysisEngineTests
{
    private static AppAnalysis Sample() => AppAnalysis.FromFolder(TestPaths.SampleApp);

    [Fact]
    public void Stats_counts_match_fixture()
    {
        var s = StatsEngine.Compute(Sample());
        Assert.Equal(2, s.ScreenCount);
        Assert.Equal(6, s.ControlCount);
        Assert.Equal(4, s.GlobalVariableCount);
        Assert.Equal(2, s.CollectionCount);
        Assert.Equal(1, s.NamedFormulaCount);
        Assert.True(s.MaxFormulaComplexity > 0);
    }

    [Fact]
    public void Dead_code_finds_unused_but_not_the_home_screen()
    {
        var r = AnalyzeEngine.DeadCode(Sample());
        Assert.Contains(r.UnusedVariables, d => d.Name == "gblUnused");
        Assert.Contains(r.UnusedCollections, d => d.Name == "colOrphan");
        Assert.Contains(r.UnusedMedia, m => m.Name == "logo");
        Assert.Empty(r.UnreachableScreens); // scrHome is the entry (outgoing nav), not orphaned
    }

    [Fact]
    public void Impact_of_collection_includes_its_readers()
    {
        var nodes = AnalyzeEngine.Impact(Sample(), "colItems");
        var names = nodes.Select(n => n.Name).ToHashSet();
        Assert.Contains("galItems", names);
        Assert.Contains("btnNoLabel", names);
    }

    [Fact]
    public void Diff_detects_rename_removal_and_property_change()
    {
        var baseModel = AppModelBuilder.Build(YamlLoader.LoadFolder(TestPaths.SampleApp));

        var tmp = Path.Combine(Path.GetTempPath(), "easel-difftest-" + Guid.NewGuid().ToString("n"));
        try
        {
            CopyDir(TestPaths.SampleApp, tmp);
            var screen = Path.Combine(tmp, "Src", "scrHome.pa.yaml");
            var text = File.ReadAllText(screen);
            text = text.Replace("btnGo:", "btnOpen:")
                       .Replace("Text: =AppTitle", "Text: =\"Dashboard\"");
            var cut = text.IndexOf("      - txtSearch:", StringComparison.Ordinal);
            if (cut > 0) text = text[..cut];
            File.WriteAllText(screen, text);

            var headModel = AppModelBuilder.Build(YamlLoader.LoadFolder(tmp));
            var diff = AppDiffEngine.Diff(baseModel, headModel);

            Assert.Contains(diff.Changes, c => c.Kind == ChangeKind.Renamed && c.Name.Contains("btnOpen"));
            Assert.Contains(diff.Changes, c => c.Kind == ChangeKind.Removed && c.Name == "txtSearch");
            Assert.Contains(diff.Changes, c => c.Kind == ChangeKind.PropertyChanged && c.Name.Contains("lblTitle"));
        }
        finally
        {
            if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true);
        }
    }

    private static void CopyDir(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(from, to));
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var dest = file.Replace(from, to);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }
}
