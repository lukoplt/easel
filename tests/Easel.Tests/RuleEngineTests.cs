using Easel.Core;
using Easel.Core.Config;
using Easel.Core.Model;
using Easel.Rules;
using Xunit;
using Xunit.Abstractions;

namespace Easel.Tests;

public sealed class RuleEngineTests
{
    private readonly ITestOutputHelper _out;
    public RuleEngineTests(ITestOutputHelper o) => _out = o;

    private IReadOnlyList<Finding> RunRules(EaselConfig? cfg = null)
    {
        var analysis = AppAnalysis.FromFolder(TestPaths.SampleApp);
        var engine = RuleEngine.CreateDefault();
        return engine.Run(analysis, cfg ?? EaselConfig.Empty);
    }

    [Fact]
    public void Discovers_all_builtin_rules()
    {
        var engine = RuleEngine.CreateDefault();
        var ids = engine.Rules.Select(r => r.Id).ToHashSet();
        _out.WriteLine("rules: " + string.Join(", ", ids.OrderBy(x => x)));
        foreach (var expected in new[] { "PF0001", "PA1003", "PA1004", "PA1005", "PA1006", "PA1008", "PA1009", "PA1010" })
            Assert.Contains(expected, ids);
    }

    [Fact]
    public void Flags_unused_variables_and_collections()
    {
        var f = RunRules().Where(x => x.RuleId == "PA1003").ToList();
        Assert.Contains(f, x => x.Message.Contains("gblUnused"));
        Assert.Contains(f, x => x.Message.Contains("colOrphan"));
        Assert.DoesNotContain(f, x => x.Message.Contains("gblUser"));
        Assert.DoesNotContain(f, x => x.Message.Contains("colItems"));
    }

    [Fact]
    public void Flags_unused_media()
    {
        var f = RunRules().Where(x => x.RuleId == "PA1004").ToList();
        Assert.Contains(f, x => x.Message.Contains("logo"));
    }

    [Fact]
    public void Flags_missing_accessible_label_only_where_missing()
    {
        var f = RunRules().Where(x => x.RuleId == "PA1009").ToList();
        Assert.Contains(f, x => x.Message.Contains("btnNoLabel"));
        Assert.DoesNotContain(f, x => x.Message.Contains("btnGo")); // has AccessibleLabel
    }

    [Fact]
    public void Flags_hardcoded_colors()
    {
        var f = RunRules().Where(x => x.RuleId == "PA1008").ToList();
        Assert.NotEmpty(f); // lblTitle.Color and scrDetail.Fill use RGBA literals
    }

    [Fact]
    public void Flags_deep_nested_if()
    {
        var f = RunRules().Where(x => x.RuleId == "PA1010").ToList();
        Assert.Contains(f, x => x.ElementPath!.Contains("btnNoLabel"));
    }

    [Fact]
    public void No_parse_errors_in_clean_fixture()
    {
        Assert.DoesNotContain(RunRules(), x => x.RuleId == "PF0001");
    }

    [Fact]
    public void Config_can_disable_a_rule()
    {
        var yaml = "rules:\n  unused-variable: off\n";
        var cfg = ConfigLoaderInline(yaml);
        Assert.DoesNotContain(RunRules(cfg), x => x.RuleId == "PA1003");
    }

    [Fact]
    public void Config_can_override_severity()
    {
        var yaml = "rules:\n  unused-media:\n    severity: error\n";
        var cfg = ConfigLoaderInline(yaml);
        var f = RunRules(cfg).Where(x => x.RuleId == "PA1004").ToList();
        Assert.NotEmpty(f);
        Assert.All(f, x => Assert.Equal(Severity.Error, x.Severity));
    }

    private static EaselConfig ConfigLoaderInline(string yaml)
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".easel.yml");
        File.WriteAllText(tmp, yaml);
        try { return ConfigLoader.Load(tmp); }
        finally { File.Delete(tmp); }
    }
}
