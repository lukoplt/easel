using Easel.Analysis.Diff;
using Easel.Core;
using Easel.Core.Config;
using Easel.Core.Model;
using Easel.Rules;
using Xunit;
using AppDiff = Easel.Analysis.Diff.DiffEngine;

namespace Easel.Tests;

/// <summary>Locks in the correctness fixes from the code review.</summary>
public sealed class P2FixTests
{
    private static (AppAnalysis Analysis, string Dir) Build(params (string Name, string Content)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "easel-p2-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(dir, "Src"));
        foreach (var (name, content) in files)
            File.WriteAllText(Path.Combine(dir, "Src", name), content);
        return (AppAnalysis.FromFolder(dir), dir);
    }

    private static IReadOnlyList<Finding> Lint((AppAnalysis Analysis, string Dir) app) =>
        app.Analysis.LoadFindings().Concat(RuleEngine.CreateDefault().Run(app.Analysis, EaselConfig.Empty)).ToList();

    private const string MinimalApp = "App:\n  Properties:\n    OnStart: =true\n";

    [Fact]
    public void Accessibility_flags_empty_or_blank_label()
    {
        var app = Build(
            ("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml",
             "Screens:\n  scr:\n    Children:\n" +
             "      - btnEmpty:\n          Control: Classic/Button@2.2.0\n          Properties:\n            AccessibleLabel: =\"\"\n            OnSelect: =true\n" +
             "      - btnBlank:\n          Control: Classic/Button@2.2.0\n          Properties:\n            AccessibleLabel: =Blank()\n            OnSelect: =true\n"));
        try
        {
            var pa1009 = Lint(app).Where(f => f.RuleId == "PA1009").ToList();
            Assert.Contains(pa1009, f => f.Message.Contains("btnEmpty"));
            Assert.Contains(pa1009, f => f.Message.Contains("btnBlank"));
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Secrets_found_in_unparsable_formula()
    {
        // A broken formula the parser rejects still gets its raw value scanned.
        var app = Build(
            ("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml",
             "Screens:\n  scr:\n    Children:\n" +
             "      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: =Set(broken, \"AKIAIOSFODNN7EXAMPLE\"\n"));
        try
        {
            var ids = Lint(app).Select(f => f.RuleId).ToHashSet();
            Assert.Contains("PF0001", ids);  // formula is unparsable…
            Assert.Contains("PA2001", ids);  // …but the secret is still caught
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void NPlusOne_ignores_data_op_in_the_source_argument()
    {
        var noHit = Build(
            ("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml",
             "Screens:\n  scr:\n    Children:\n" +
             "      - gal:\n          Control: Gallery@2.15.0\n          Properties:\n            Items: =ForAll(LookUp(Accounts, true).Items, Value)\n"));
        try
        {
            Assert.DoesNotContain(Lint(noHit), f => f.RuleId == "PA1002");
        }
        finally { Directory.Delete(noHit.Dir, true); }

        var hit = Build(
            ("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml",
             "Screens:\n  scr:\n    Children:\n" +
             "      - btn:\n          Control: Classic/Button@2.2.0\n          Properties:\n            AccessibleLabel: =\"x\"\n            OnSelect: =ForAll(colX, Collect(colY, Value))\n"));
        try
        {
            Assert.Contains(Lint(hit), f => f.RuleId == "PA1002");
        }
        finally { Directory.Delete(hit.Dir, true); }
    }

    [Fact]
    public void Delegation_does_not_flag_unknown_symbols()
    {
        var app = Build(
            ("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml",
             "Screens:\n  scr:\n    Children:\n" +
             "      - gal:\n          Control: Gallery@2.15.0\n          Properties:\n            Items: =Filter(SomeUnknownThing, CountRows(colX) > 0)\n"));
        try
        {
            Assert.DoesNotContain(Lint(app), f => f.RuleId == "PA1001");
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Diff_distinguishes_whitespace_inside_strings()
    {
        var v1 = Build(("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Children:\n      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: =\"a b\"\n"));
        var v2 = Build(("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Children:\n      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: =\"ab\"\n"));
        try
        {
            var diff = AppDiff.Diff(v1.Analysis.Model, v2.Analysis.Model);
            Assert.Contains(diff.Changes, c => c.Kind == ChangeKind.PropertyChanged && c.Name.Contains("lbl.Text"));
        }
        finally { Directory.Delete(v1.Dir, true); Directory.Delete(v2.Dir, true); }
    }

    [Fact]
    public void Diff_reports_added_and_removed_properties()
    {
        var v1 = Build(("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Children:\n      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: =\"hi\"\n            Color: =RGBA(0,0,0,1)\n"));
        var v2 = Build(("App.pa.yaml", MinimalApp),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Children:\n      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: =\"hi\"\n            X: =10\n"));
        try
        {
            var diff = AppDiff.Diff(v1.Analysis.Model, v2.Analysis.Model);
            Assert.Contains(diff.Changes, c => c.Kind == ChangeKind.Added && c.Name.Contains("lbl.X"));
            Assert.Contains(diff.Changes, c => c.Kind == ChangeKind.Removed && c.Name.Contains("lbl.Color"));
        }
        finally { Directory.Delete(v1.Dir, true); Directory.Delete(v2.Dir, true); }
    }
}
