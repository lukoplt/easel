using Easel.Core;
using Easel.Core.Config;
using Easel.Core.Model;
using Easel.Rules;
using Xunit;

namespace Easel.Tests;

/// <summary>End-to-end coverage: each rule fires on the fixture designed to trip it.</summary>
public sealed class RuleCoverageTests
{
    private static IReadOnlyList<Finding> Lint(string fixture, string? configYaml = null)
    {
        var analysis = AppAnalysis.FromFolder(Path.Combine(TestPaths.FixturesDir, fixture));
        var cfg = configYaml is null ? EaselConfig.Empty : InlineConfig(configYaml);
        return analysis.LoadFindings().Concat(RuleEngine.CreateDefault().Run(analysis, cfg)).ToList();
    }

    [Theory]
    [InlineData("PA1001")] // non-delegable query over Orders
    [InlineData("PA1002")] // ForAll + Patch
    [InlineData("PA1003")] // unused gblA..gblD
    [InlineData("PA1004")] // icon.png unused ("iconography" is not a token match)
    [InlineData("PA1006")] // heavy OnStart
    [InlineData("PA1008")] // RGBA literal
    [InlineData("PA1011")] // timer side effect
    [InlineData("PA1012")] // duplicate Concatenate
    [InlineData("PA1013")] // Label 2.5.1 vs 2.4.0
    [InlineData("PA1014")] // nested ForAll in btnNested
    [InlineData("PA1015")] // repeated LookUp in lblRepeat
    [InlineData("PA1016")] // CountRows(Filter(...)) in lblStats
    [InlineData("PA1017")] // First(Filter(...)) in lblFirstOpen
    [InlineData("PA1018")] // two sequential ClearCollects in App.OnStart
    [InlineData("PA1019")] // scrArchive/lblRemote reads btnSync.Text cross-screen
    [InlineData("PA1020")] // btnSync FocusedBorderThickness 0
    [InlineData("PA1021")] // vidPromo without ClosedCaptionsUrl
    [InlineData("PA1022")] // default screen name Screen1
    [InlineData("PA1023")] // lblDup1 TabIndex 2
    [InlineData("PA1024")] // vidPromo AutoStart true
    [InlineData("PA1025")] // tglNotify ShowValue false
    [InlineData("PA1026")] // penSign with no TextInput on scrArchive
    [InlineData("PA1027")] // scrArchive/Screen1 never referenced (StartScreen set)
    [InlineData("PA1028")] // txtFilter drives Filter without DelayOutput
    [InlineData("PA1030")] // lblLowContrast grey-on-white
    [InlineData("PA1031")] // Unused.json data source never referenced
    [InlineData("PF0002")] // Navigate(scrMissing) — screen does not exist
    [InlineData("PF0003")] // txtFiltr.Text — one edit from txtFilter
    [InlineData("PF0004")] // Left("abc") — needs 2 arguments
    [InlineData("PF0005")] // Concatt(...) — unknown function
    [InlineData("PA2001")] // AKIA... key
    [InlineData("PA2003")] // url with creds
    [InlineData("PF0001")] // =Set(broken,
    public void ComponentApp_trips_rule(string ruleId)
    {
        var ids = Lint("ComponentApp").Select(f => f.RuleId).ToHashSet();
        Assert.Contains(ruleId, ids);
    }

    [Fact]
    public void CleanApp_has_no_findings()
    {
        Assert.Empty(Lint("CleanApp"));
    }

    [Fact]
    public void PA1005_fires_when_limit_lowered()
    {
        var f = Lint("ComponentApp", "rules:\n  screen-control-limit:\n    max: 2\n");
        Assert.Contains(f, x => x.RuleId == "PA1005");
    }

    [Fact]
    public void PA1029_fires_when_limit_lowered()
    {
        var f = Lint("ComponentApp", "rules:\n  large-media:\n    max-kb: 0\n");
        Assert.Contains(f, x => x.RuleId == "PA1029");
    }

    [Fact]
    public void PA1007_fires_with_strict_pattern()
    {
        var f = Lint("SampleApp", "rules:\n  naming-convention:\n    patterns:\n      variable: \"^zzz\"\n");
        Assert.Contains(f, x => x.RuleId == "PA1007");
    }

    [Fact]
    public void Broken_file_surfaces_PA0002()
    {
        var dir = Path.Combine(Path.GetTempPath(), "easel-broken-" + Guid.NewGuid().ToString("n"), "Src");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "scrX.pa.yaml"), "Screens:\n  scrX:\n    Children:\n      - a: b: c\n");
            var analysis = AppAnalysis.FromFolder(Directory.GetParent(dir)!.FullName);
            Assert.NotEmpty(analysis.LoadFindings());
            Assert.All(analysis.LoadFindings(), f => Assert.Equal("PA0002", f.RuleId));
        }
        finally { Directory.Delete(Directory.GetParent(dir)!.FullName, recursive: true); }
    }

    private static EaselConfig InlineConfig(string yaml)
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".easel.yml");
        File.WriteAllText(tmp, yaml);
        try { return ConfigLoader.Load(tmp); }
        finally { File.Delete(tmp); }
    }
}
