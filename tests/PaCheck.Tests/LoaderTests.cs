using PaCheck.Core.Loader;
using PaCheck.Core.Model;
using Xunit;

namespace PaCheck.Tests;

public sealed class LoaderTests
{
    private static AppModel LoadSample()
    {
        var load = YamlLoader.LoadFolder(TestPaths.SampleApp);
        Assert.Empty(load.Diagnostics);
        return AppModelBuilder.Build(load);
    }

    [Fact]
    public void Loads_app_screens_and_controls()
    {
        var model = LoadSample();

        Assert.Equal(2, model.Screens.Count);
        Assert.Contains(model.Screens, s => s.Name == "scrHome");
        Assert.Contains(model.Screens, s => s.Name == "scrDetail");

        var home = model.Screens.Single(s => s.Name == "scrHome");
        var controlNames = home.AllControls().Select(c => c.Name).ToList();
        Assert.Contains("galItems", controlNames);
        Assert.Contains("btnGo", controlNames);
        Assert.Contains("btnNoLabel", controlNames);
    }

    [Fact]
    public void Parses_control_type_and_version()
    {
        var model = LoadSample();
        var home = model.Screens.Single(s => s.Name == "scrHome");
        var btn = home.AllControls().Single(c => c.Name == "btnGo");

        Assert.Equal("Classic/Button", btn.ControlType);
        Assert.Equal("2.2.0", btn.Version);
    }

    [Fact]
    public void Strips_equals_and_flags_formulas()
    {
        var model = LoadSample();
        var home = model.Screens.Single(s => s.Name == "scrHome");
        var btn = home.AllControls().Single(c => c.Name == "btnGo");

        var onSelect = btn.GetProperty("OnSelect")!;
        Assert.True(onSelect.IsFormula);
        Assert.StartsWith("Navigate(scrDetail", onSelect.Formula);
    }

    [Fact]
    public void Reads_app_onstart_and_named_formula()
    {
        var model = LoadSample();

        var onStart = model.App.GetProperty("OnStart")!;
        Assert.Contains("Set(gblUser", onStart.Formula);
        Assert.Contains("ClearCollect(colItems", onStart.Formula);

        Assert.Contains(model.App.Formulas, f => f.Name == "AppTitle");
    }

    [Fact]
    public void Discovers_media_asset()
    {
        var model = LoadSample();
        Assert.Contains(model.Media, m => m.Name == "logo");
    }

    [Fact]
    public void Every_element_carries_a_source_location()
    {
        var model = LoadSample();
        var home = model.Screens.Single(s => s.Name == "scrHome");
        var btn = home.AllControls().Single(c => c.Name == "btnNoLabel");

        Assert.True(btn.Location.IsKnown);
        Assert.EndsWith("scrHome.pa.yaml", btn.Location.File.Replace('\\', '/'));
        Assert.True(btn.Location.Line > 0);
    }
}
