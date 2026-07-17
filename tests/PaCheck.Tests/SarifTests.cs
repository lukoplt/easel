using System.Text.Json;
using PaCheck.Core;
using PaCheck.Core.Config;
using PaCheck.Output;
using PaCheck.Rules;
using Xunit;

namespace PaCheck.Tests;

/// <summary>Structural validation of the SARIF 2.1.0 output (offline substitute for code-scanning).</summary>
public sealed class SarifTests
{
    private static JsonElement RenderSarif(string fixture)
    {
        var a = AppAnalysis.FromFolder(Path.Combine(TestPaths.FixturesDir, fixture));
        var findings = RuleEngine.CreateDefault().Run(a, PaCheckConfig.Empty);
        var report = new LintReport("pacheck", "0.1.0", "1.0", fixture, findings);
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
        Assert.Equal("pacheck", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
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
            Assert.True(r.GetProperty("partialFingerprints").TryGetProperty("pacheck/v1", out _));
        }
    }

    [Fact]
    public void Clean_app_yields_empty_results()
    {
        var run = RenderSarif("CleanApp").GetProperty("runs")[0];
        Assert.Equal(0, run.GetProperty("results").GetArrayLength());
    }
}
