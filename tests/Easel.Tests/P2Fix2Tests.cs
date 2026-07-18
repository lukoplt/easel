using Easel.Analysis.Diff;
using Easel.Analysis.Rename;
using Easel.Core;
using Easel.Core.Config;
using Easel.Core.Model;
using Easel.Rules;
using Xunit;
using AppDiff = Easel.Analysis.Diff.DiffEngine;

namespace Easel.Tests;

/// <summary>Locks in the second round of code-review fixes.</summary>
public sealed class P2Fix2Tests
{
    private static (AppAnalysis Analysis, string Dir) Build(params (string Name, string Content)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "easel-p2b-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(dir, "Src"));
        foreach (var (name, content) in files)
            File.WriteAllText(Path.Combine(dir, "Src", name), content);
        return (AppAnalysis.FromFolder(dir), dir);
    }

    private static EaselConfig InlineConfig(string yaml)
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".easel.yml");
        File.WriteAllText(tmp, yaml);
        try { return ConfigLoader.Load(tmp); }
        finally { File.Delete(tmp); }
    }

    private const string MinimalApp = "App:\n  Properties:\n    OnStart: =true\n";

    [Fact]
    public void High_entropy_not_flagged_on_identifier_expressions()
    {
        // The reported false positive: expression identifier chains must not trip PA2002.
        var app = Build(
            ("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml",
             "Screens:\n  scr:\n    Children:\n" +
             "      - lbl:\n          Control: Label@2.5.1\n          Properties:\n" +
             "            Text: =DropdownCity_2.SelectedText.Value & TextCanvas3_1.Height\n"));
        try
        {
            var f = RuleEngine.CreateDefault().Run(app.Analysis, EaselConfig.Empty);
            Assert.DoesNotContain(f, x => x.RuleId == "PA2002");
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Allowlist_suppresses_only_the_matched_secret()
    {
        var app = Build(
            ("App.pa.yaml",
             "App:\n  Properties:\n    OnStart: |\n" +
             "      =Set(u, \"https://ok.example.com/api\");\n" +
             "      Set(k, \"AKIAIOSFODNN7EXAMPLE\")\n"),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Properties:\n      Fill: =true\n"));
        try
        {
            var cfg = InlineConfig("rules:\n  hardcoded-secret:\n    allowlist:\n      - \"ok.example.com\"\n");
            var f = RuleEngine.CreateDefault().Run(app.Analysis, cfg);
            // The allowlisted URL is suppressed, but the real API key is still reported.
            Assert.Contains(f, x => x.RuleId == "PA2001");
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Rename_is_ast_precise_and_leaves_string_literals_intact()
    {
        var app = Build(
            ("App.pa.yaml", "App:\n  Properties:\n    OnStart: =Set(gblFoo, 1)\n"),
            ("scr.pa.yaml",
             "Screens:\n  scr:\n    Children:\n" +
             "      - lbl:\n          Control: Label@2.5.1\n          Properties:\n" +
             "            Text: =gblFoo & \" gblFoo stays \"\n"));
        try
        {
            var result = RenameEngine.Rename(app.Dir, "gblFoo", "gblBar", app.Analysis);
            Assert.True(result.Success, result.Message);

            var text = File.ReadAllText(Path.Combine(app.Dir, "Src", "scr.pa.yaml"));
            Assert.Contains("gblBar", text);              // identifier renamed
            Assert.Contains("gblFoo stays", text);        // string literal untouched
            Assert.True(result.StringLiteralHits >= 1);   // and reported
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Diff_distinguishes_single_quoted_identifier_whitespace()
    {
        var v1 = Build(("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Children:\n      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: ='a b'\n"));
        var v2 = Build(("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Children:\n      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: ='ab'\n"));
        try
        {
            var diff = AppDiff.Diff(v1.Analysis.Model, v2.Analysis.Model);
            Assert.Contains(diff.Changes, c => c.Kind == ChangeKind.PropertyChanged && c.Name.Contains("lbl.Text"));
        }
        finally { Directory.Delete(v1.Dir, true); Directory.Delete(v2.Dir, true); }
    }

    [Fact]
    public void Rename_refuses_structural_control_rename()
    {
        var app = Build(
            ("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Children:\n      - btnX:\n          Control: Classic/Button@2.2.0\n          Properties:\n            OnSelect: =true\n"));
        try
        {
            var result = RenameEngine.Rename(app.Dir, "btnX", "btnY", app.Analysis);
            Assert.False(result.Success);
            Assert.Contains("Control", result.Message);
        }
        finally { Directory.Delete(app.Dir, true); }
    }
}
