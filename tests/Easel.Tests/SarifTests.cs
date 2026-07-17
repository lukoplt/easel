using System.Text.Json;
using Easel.Core;
using Easel.Core.Config;
using Easel.Output;
using Easel.Rules;
using Xunit;

namespace Easel.Tests;

/// <summary>Structural validation of the SARIF 2.1.0 output (offline substitute for code-scanning).</summary>
public sealed class SarifTests
{
    private static JsonElement RenderSarif(string fixture)
    {
        var a = AppAnalysis.FromFolder(Path.Combine(TestPaths.FixturesDir, fixture));
        var findings = RuleEngine.CreateDefault().Run(a, EaselConfig.Empty);
        var report = new LintReport("easel", "0.1.0", "1.0", fixture, findings);
        var json = new SarifReportRenderer().Render(report);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void Sarif_has_required_top_level_shape()
    {
        var root = RenderSarif("ComponentApp");
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        Assert.True(root.TryGetProperty("$schema", out _));

        var run = root.GetProperty("runs")[0];
        Assert.Equal("easel", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
        Assert.True(run.GetProperty("tool").GetProperty("driver").GetProperty("rules").GetArrayLength() > 0);
    }

    [Fact]
    public void Every_result_has_ruleId_valid_level_and_fingerprint()
    {
        var run = RenderSarif("ComponentApp").GetProperty("runs")[0];
        var results = run.GetProperty("results");
        Assert.True(results.GetArrayLength() > 0);

        var validLevels = new[] { "error", "warning", "note" };
        foreach (var r in results.EnumerateArray())
        {
            Assert.False(string.IsNullOrEmpty(r.GetProperty("ruleId").GetString()));
            Assert.Contains(r.GetProperty("level").GetString(), validLevels);
            Assert.False(string.IsNullOrEmpty(r.GetProperty("message").GetProperty("text").GetString()));
            Assert.True(r.GetProperty("partialFingerprints").TryGetProperty("easel/v1", out _));
        }
    }

    [Fact]
    public void Clean_app_yields_empty_results()
    {
        var run = RenderSarif("CleanApp").GetProperty("runs")[0];
        Assert.Equal(0, run.GetProperty("results").GetArrayLength());
    }
}
