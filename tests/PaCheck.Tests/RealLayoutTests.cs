using PaCheck.Core;
using Xunit;

namespace PaCheck.Tests;

/// <summary>
/// Regression: a real pac solution unpack puts modern sources under Other/Src/*.pa.yaml
/// (with legacy Src/*.fx.yaml and editor-state files alongside). The loader must find the
/// pa.yaml wherever it lives and never ingest editor-state or legacy .fx.yaml.
/// </summary>
public sealed class RealLayoutTests
{
    private static AppAnalysis Analyze() =>
        AppAnalysis.FromFolder(Path.Combine(TestPaths.FixturesDir, "SolutionLayoutApp"));

    [Fact]
    public void Finds_screens_under_other_src()
    {
        var a = Analyze();
        Assert.Contains(a.Model.Screens, s => s.Name == "Home");
        Assert.Contains(a.Model.Screens.SelectMany(s => s.AllControls()), c => c.Name == "lblReal");
    }

    [Fact]
    public void Ignores_legacy_fx_yaml()
    {
        var a = Analyze();
        Assert.DoesNotContain(a.Model.AllControls(), c => c.Name == "lblLegacy");
    }

    [Fact]
    public void Ignores_editor_state()
    {
        var a = Analyze();
        Assert.DoesNotContain(a.Model.AllControls(), c => c.Name == "ghostControl");
        Assert.Empty(a.LoadFindings()); // no PA0002 from the editor-state file
    }

    [Fact]
    public void Reads_app_onstart_from_other_src()
    {
        var a = Analyze();
        Assert.Contains("gblReal", a.Model.App.GetProperty("OnStart")!.Formula);
    }
}
