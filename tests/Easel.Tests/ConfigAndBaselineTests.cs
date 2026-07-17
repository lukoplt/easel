using Easel.Core.Baseline;
using Easel.Core.Config;
using Easel.Core.Model;
using Easel.Rules;
using Xunit;

namespace Easel.Tests;

public sealed class ConfigAndBaselineTests
{
    [Theory]
    [InlineData("Src/App.pa.yaml", "**/*.pa.yaml", true)]
    [InlineData("Src/App.pa.yaml", "**/App.pa.yaml", true)]
    [InlineData("Src/LegacyScreen.pa.yaml", "**/Legacy*.pa.yaml", true)]
    [InlineData("Src/scrHome.pa.yaml", "**/Legacy*.pa.yaml", false)]
    [InlineData("a/b/c.txt", "a/*/c.txt", true)]
    [InlineData("a/b/d/c.txt", "a/*/c.txt", false)]
    public void Glob_matches(string path, string pattern, bool expected) =>
        Assert.Equal(expected, Glob.IsMatch(path, pattern));

    [Fact]
    public void Config_search_walks_upward()
    {
        var root = Path.Combine(Path.GetTempPath(), "easel-cfg-" + Guid.NewGuid().ToString("n"));
        var deep = Path.Combine(root, "a", "b", "c");
        Directory.CreateDirectory(deep);
        try
        {
            File.WriteAllText(Path.Combine(root, ".easel.yml"), "rules:\n  unused-media: off\n");
            var found = ConfigLoader.Find(deep);
            Assert.NotNull(found);

            var cfg = ConfigLoader.Load(found);
            Assert.False(cfg.ForRule("PA1004", "unused-media").Enabled);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Baseline_round_trips_and_suppresses_known_findings()
    {
        var f1 = new Finding("PA1003", "unused-variable", RuleCategory.Maintainability, Severity.Warning,
            "Variable 'x' unused.", new SourceLocation("a.pa.yaml", 1, 1), "scr/x");
        var f2 = new Finding("PA1009", "missing-accessible-label", RuleCategory.Accessibility, Severity.Warning,
            "no label", new SourceLocation("b.pa.yaml", 2, 2), "scr/btn");

        var path = Path.Combine(Path.GetTempPath(), "easel-baseline-" + Guid.NewGuid().ToString("n") + ".json");
        try
        {
            Baseline.FromFindings(new[] { f1 }).Save(path);
            var loaded = Baseline.Load(path);

            var remaining = loaded.Filter(new[] { f1, f2 });
            Assert.Single(remaining);
            Assert.Equal("PA1009", remaining[0].RuleId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Fingerprint_is_line_independent()
    {
        var a = new Finding("PA1003", "unused-variable", RuleCategory.Maintainability, Severity.Warning,
            "Variable 'x' unused.", new SourceLocation("a.pa.yaml", 5, 1), "scr/x");
        var b = a with { Location = new SourceLocation("a.pa.yaml", 99, 3) };
        Assert.Equal(a.Fingerprint(), b.Fingerprint()); // moving lines does not change identity
    }
}
