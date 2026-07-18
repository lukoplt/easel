using System.Diagnostics;
using Easel.Analysis.Analyze;
using Easel.Analysis.Diff;
using Easel.Analysis.Rename;
using Easel.Analysis.Secrets;
using Easel.Cli.Infrastructure;
using Easel.Core;
using Easel.Core.Config;
using Easel.Core.Model;
using Easel.Core.Security;
using Easel.Pac;
using Easel.Rules;
using AppDiff = Easel.Analysis.Diff.DiffEngine;

namespace Easel.Tests;

public sealed class ReviewFindingsRegressionTests
{
    private static (AppAnalysis Analysis, string Dir) Build(params (string Name, string Content)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "easel-review-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(dir, "Src"));
        foreach (var (name, content) in files)
            File.WriteAllText(Path.Combine(dir, "Src", name), content);
        return (AppAnalysis.FromFolder(dir), dir);
    }

    private static EaselConfig InlineConfig(string yaml)
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".easel.yml");
        File.WriteAllText(file, yaml);
        try { return ConfigLoader.Load(file); }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Rename_updates_named_formula_definition_and_preserves_source_formatting()
    {
        const string source =
            "# header remains\n" +
            "App:\n" +
            "  Formulas:\n" +
            "    'OldFormula': =1\n" +
            "  Properties:\n" +
            "    OnStart: |\n" +
            "      =Set(gblResult, OldFormula); Set(gblRecord, {OldFormula: 2}); Set(gblField, gblRecord.OldFormula) # inline remains\n";
        const string expected =
            "# header remains\n" +
            "App:\n" +
            "  Formulas:\n" +
            "    'NewFormula': =1\n" +
            "  Properties:\n" +
            "    OnStart: |\n" +
            "      =Set(gblResult, NewFormula); Set(gblRecord, {OldFormula: 2}); Set(gblField, gblRecord.OldFormula) # inline remains\n";
        var app = Build(("App.pa.yaml", source));
        try
        {
            var result = RenameEngine.Rename(app.Dir, "OldFormula", "NewFormula", app.Analysis);
            Assert.True(result.Success, result.Message);
            var actual = File.ReadAllText(Path.Combine(app.Dir, "Src", "App.pa.yaml"));
            Assert.Equal(expected, actual);

            var reloaded = AppAnalysis.FromFolder(app.Dir);
            Assert.True(reloaded.Symbols.IsDefined("NewFormula"));
            Assert.False(reloaded.Symbols.IsDefined("OldFormula"));
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Rename_restores_changed_files_when_a_later_yaml_file_is_invalid()
    {
        const string source =
            "App:\n" +
            "  Formulas:\n" +
            "    OldFormula: =1\n" +
            "  Properties:\n" +
            "    OnStart: =OldFormula\n";
        var app = Build(
            ("App.pa.yaml", source),
            ("broken.pa.yaml", "Screens:\n  broken: [\n"));
        try
        {
            Assert.ThrowsAny<YamlDotNet.Core.YamlException>(() =>
                RenameEngine.Rename(app.Dir, "OldFormula", "NewFormula", app.Analysis));
            Assert.Equal(source, File.ReadAllText(Path.Combine(app.Dir, "Src", "App.pa.yaml")));
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Rename_rejects_case_only_no_op_without_touching_source()
    {
        const string source = "App:\n  Formulas:\n    OldFormula: =1\n";
        var app = Build(("App.pa.yaml", source));
        try
        {
            var result = RenameEngine.Rename(app.Dir, "OldFormula", "oldformula", app.Analysis);
            Assert.False(result.Success);
            Assert.Equal(source, File.ReadAllText(Path.Combine(app.Dir, "Src", "App.pa.yaml")));
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Rename_rejects_invalid_unsupported_and_missing_symbols()
    {
        var app = Build(
            ("App.pa.yaml", "App:\n  Properties:\n    OnStart: =true\n"),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Children:\n      - lbl:\n          Control: Label@2.5.1\n"));
        try
        {
            Assert.False(RenameEngine.Rename(app.Dir, "missing", "bad-name", app.Analysis).Success);
            Assert.False(RenameEngine.Rename(app.Dir, "lbl", "newLabel", app.Analysis).Success);
            Assert.False(RenameEngine.Rename(app.Dir, "missing", "validName", app.Analysis).Success);
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Rename_updates_context_definition_and_reads_in_block_scalar()
    {
        const string screen =
            "Screens:\n" +
            "  scr:\n" +
            "    Properties:\n" +
            "      OnVisible: |\n" +
            "        =UpdateContext({locOld: 1});\n" +
            "        Set(gblCopy, locOld)\n";
        var app = Build(("App.pa.yaml", "App:\n  Properties:\n    OnStart: =true\n"), ("scr.pa.yaml", screen));
        try
        {
            var result = RenameEngine.Rename(app.Dir, "locOld", "locNew", app.Analysis);
            Assert.True(result.Success, result.Message);
            var actual = File.ReadAllText(Path.Combine(app.Dir, "Src", "scr.pa.yaml"));
            Assert.Equal(screen.Replace("locOld", "locNew"), actual);

            var reloaded = AppAnalysis.FromFolder(app.Dir);
            Assert.True(reloaded.Symbols.IsDefined("locNew"));
            Assert.False(reloaded.Symbols.IsDefined("locOld"));
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Rename_updates_reference_in_double_quoted_yaml_formula()
    {
        const string appSource = "App:\n  Properties:\n    OnStart: =Set(gblOld, 1)\n";
        const string screenSource = "Screens:\n  scr:\n    Properties:\n      Visible: \"=gblOld\"\n";
        var app = Build(("App.pa.yaml", appSource), ("scr.pa.yaml", screenSource));
        try
        {
            var result = RenameEngine.Rename(app.Dir, "gblOld", "gblNew", app.Analysis);
            Assert.True(result.Success, result.Message);
            Assert.Equal(screenSource.Replace("gblOld", "gblNew"),
                File.ReadAllText(Path.Combine(app.Dir, "Src", "scr.pa.yaml")));
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Valid_identifier_comparison_is_not_reported_as_a_secret()
    {
        var app = Build(
            ("App.pa.yaml", "App:\n  Properties:\n    OnStart: =true\n"),
            ("scr.pa.yaml", "Screens:\n  scr:\n    Properties:\n      Visible: =If(api_key = DropdownSelectedValue, true, false)\n"));
        try
        {
            var findings = RuleEngine.CreateDefault().Run(app.Analysis, EaselConfig.Empty);
            Assert.DoesNotContain(findings, f => f.RuleId == "PA2001");
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Broken_formula_keeps_entropy_detection_and_checks_every_secret()
    {
        const string first = "AKIAIOSFODNN7EXAMPLE";
        const string second = "AKIA1234567890ABCDEF";
        const string random = "f9Kd82hLzQ0pV3mNx7Tb5RwYcA1eUj4";
        var app = Build(
            ("App.pa.yaml", "App:\n  Properties:\n    OnStart: =true\n"),
            ("scr.pa.yaml", $"Screens:\n  scr:\n    Properties:\n      Fill: =Set(broken, \"{first}\", \"{second}\", \"{random}\"\n"));
        try
        {
            var config = InlineConfig($"rules:\n  hardcoded-secret:\n    allowlist:\n      - \"{first}\"\n      - \"\"\n");
            var findings = RuleEngine.CreateDefault().Run(app.Analysis, config);
            Assert.Contains(findings, f => f.RuleId == "PA2001" && f.Message.Contains("AK…EF"));
            Assert.Contains(findings, f => f.RuleId == "PA2002");

            var standalone = SecretsScanner.Scan(app.Analysis,
                SecretScanOptions.Default with { Allowlist = new[] { first, "" } });
            Assert.Contains(standalone.Findings, f => f.RuleId == "PA2001");
            Assert.Contains(standalone.Findings, f => f.RuleId == "PA2002");
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Non_formula_property_keeps_full_entropy_detection()
    {
        const string secret = "AKIA1234567890ABCDEF";
        var app = Build(
            ("App.pa.yaml", "App:\n  Properties:\n    OnStart: =true\n"),
            ("scr.pa.yaml", $"Screens:\n  scr:\n    Properties:\n      Text: {secret}\n"));
        try
        {
            var lint = RuleEngine.CreateDefault().Run(app.Analysis, EaselConfig.Empty);
            Assert.Contains(lint, finding => finding.RuleId == "PA2001");
            Assert.Contains(SecretsScanner.Scan(app.Analysis).Findings,
                finding => finding.RuleId == "PA2001");

            var entropy = SecretDetectors.ScanProperty(
                "f9Kd82hLzQ0pV3mNx7Tb5RwYcA1eUj4",
                Array.Empty<string>(),
                isFormula: false,
                parseSucceeded: false);
            Assert.Contains(entropy, match => match.Kind == SecretKind.HighEntropy);
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Broken_formula_lexically_recovers_doubled_quotes()
    {
        var matches = SecretDetectors.ScanProperty(
            "Set(x, \"f9Kd82hLzQ0pV3mNx7Tb5RwYcA1eUj4\"\"suffix\"",
            Array.Empty<string>(),
            isFormula: true,
            parseSucceeded: false);

        Assert.Contains(matches, match => match.Kind == SecretKind.HighEntropy);
    }

    [Fact]
    public void Diff_keeps_same_named_controls_in_different_scopes()
    {
        const string appYaml = "App:\n  Properties:\n    OnStart: =true\n";
        const string baseScreens =
            "Screens:\n" +
            "  first:\n    Children:\n      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: =\"before\"\n" +
            "  second:\n    Children:\n      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: =\"same\"\n";
        var before = Build(("App.pa.yaml", appYaml), ("screens.pa.yaml", baseScreens));
        var after = Build(("App.pa.yaml", appYaml),
            ("screens.pa.yaml", baseScreens.Replace("=\"before\"", "=\"after\"")));
        try
        {
            var diff = AppDiff.Diff(before.Analysis.Model, after.Analysis.Model);
            Assert.Contains(diff.Changes,
                c => c.Kind == ChangeKind.PropertyChanged && c.Name == "lbl.Text");
        }
        finally { Directory.Delete(before.Dir, true); Directory.Delete(after.Dir, true); }
    }

    [Fact]
    public void Diff_reports_unique_control_moved_between_screens()
    {
        const string appYaml = "App:\n  Properties:\n    OnStart: =true\n";
        const string control =
            "    Children:\n      - lbl:\n          Control: Label@2.5.1\n          Properties:\n            Text: =\"same\"\n";
        var before = Build(("App.pa.yaml", appYaml),
            ("screens.pa.yaml", "Screens:\n  first:\n" + control + "  second:\n"));
        var after = Build(("App.pa.yaml", appYaml),
            ("screens.pa.yaml", "Screens:\n  first:\n  second:\n" + control));
        try
        {
            var changes = AppDiff.Diff(before.Analysis.Model, after.Analysis.Model).Changes;
            Assert.Contains(changes, change => change.Kind == ChangeKind.Moved && change.Name == "lbl");
            Assert.DoesNotContain(changes, change => change.Kind == ChangeKind.Renamed && change.Name == "lbl → lbl");
        }
        finally { Directory.Delete(before.Dir, true); Directory.Delete(after.Dir, true); }
    }

    [Fact]
    public void Context_variable_usage_and_impact_are_scope_aware()
    {
        var app = Build(
            ("App.pa.yaml", "App:\n  Properties:\n    OnStart: =true\n"),
            ("screens.pa.yaml",
             "Screens:\n" +
             "  first:\n    Properties:\n      OnVisible: |\n        =UpdateContext({locX: 1})\n      Fill: =locX\n" +
             "  second:\n    Properties:\n      OnVisible: |\n        =UpdateContext({locX: 2})\n"));
        try
        {
            Assert.Contains(app.Analysis.Symbols.OfKind(Easel.Core.Symbols.SymbolKind.ContextVariable),
                d => d.Name == "locX" && d.Scope == "first");
            Assert.Contains(app.Analysis.Symbols.OfKind(Easel.Core.Symbols.SymbolKind.ContextVariable),
                d => d.Name == "locX" && d.Scope == "second");
            var unused = AnalyzeEngine.DeadCode(app.Analysis).UnusedVariables;
            Assert.Contains(unused, d => d.Name == "locX" && d.Scope == "second");
            Assert.DoesNotContain(unused, d => d.Name == "locX" && d.Scope == "first");
            Assert.Contains(AnalyzeEngine.Impact(app.Analysis, "locX"), n => n.Id == "Screen:first");
        }
        finally { Directory.Delete(app.Dir, true); }
    }

    [Fact]
    public void Process_runner_cancellation_stops_the_child_and_throws()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var stopwatch = Stopwatch.StartNew();

        if (OperatingSystem.IsWindows())
            Assert.Throws<OperationCanceledException>(() => ProcessRunner.Run(
                "cmd.exe", new[] { "/c", "ping 127.0.0.1 -n 30 > nul" }, cancellationToken: cts.Token));
        else
            Assert.Throws<OperationCanceledException>(() => ProcessRunner.Run(
                "/bin/sh", new[] { "-c", "sleep 30" }, cancellationToken: cts.Token));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Cancellation took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Process_runner_timeout_stops_the_child_and_reports_timeout()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = OperatingSystem.IsWindows()
            ? ProcessRunner.Run("cmd.exe", new[] { "/c", "ping 127.0.0.1 -n 30 > nul" }, timeoutMs: 100)
            : ProcessRunner.Run("/bin/sh", new[] { "-c", "sleep 30" }, timeoutMs: 100);

        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("timed out", result.StdErr, StringComparison.OrdinalIgnoreCase);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Timeout took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Pac_detection_propagates_cancellation()
    {
        if (OperatingSystem.IsWindows()) return;

        var dir = Path.Combine(Path.GetTempPath(), "easel-pac-cancel-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        var pac = Path.Combine(dir, "pac");
        File.WriteAllText(pac, "#!/bin/sh\nsleep 30\n");
        File.SetUnixFileMode(pac, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", dir + Path.PathSeparator + originalPath);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            Assert.Throws<OperationCanceledException>(() => PacRunner.Detect(cts.Token));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Pac_create_accepts_supported_detected_version()
    {
        if (OperatingSystem.IsWindows()) return;

        var dir = Path.Combine(Path.GetTempPath(), "easel-pac-version-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        var pac = Path.Combine(dir, "pac");
        File.WriteAllText(pac, "#!/bin/sh\necho 'Version: 1.99.0'\n");
        File.SetUnixFileMode(pac, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", dir + Path.PathSeparator + originalPath);
            var info = PacRunner.Detect();
            Assert.True(info.VersionSupported);
            Assert.NotNull(PacRunner.Create());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Command_runner_maps_cancellation_to_exit_130()
    {
        var sync = CommandRunner.Guarded(() => throw new OperationCanceledException());
        var asyncResult = await CommandRunner.GuardedAsync(
            () => Task.FromException<int>(new OperationCanceledException()));

        Assert.Equal(ExitCode.Cancelled, sync);
        Assert.Equal(ExitCode.Cancelled, asyncResult);
    }

    [Fact]
    public void Cancellation_operation_scope_is_idempotently_disposable()
    {
        var operation = AppCancellation.BeginOperation();
        operation.Dispose();
        operation.Dispose();

        Assert.False(AppCancellation.Token.IsCancellationRequested);
    }

    [Fact]
    public void Source_preparer_reuses_unpacked_folder_without_temp_cleanup()
    {
        var cleanApp = Path.Combine(TestPaths.FixturesDir, "CleanApp");
        using var prepared = SourcePreparer.Prepare(cleanApp, keepTemp: false);

        Assert.Equal(InputKind.UnpackedFolder, prepared.OriginalKind);
        Assert.Equal(cleanApp, prepared.Folder);
        Assert.False(prepared.IsTemp);
        Assert.True(Directory.Exists(prepared.Folder));
    }

    [Fact]
    public void Source_preparer_unpacks_msapp_and_removes_temp_on_dispose()
    {
        if (OperatingSystem.IsWindows()) return;

        var dir = Path.Combine(Path.GetTempPath(), "easel-source-preparer-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        var pac = Path.Combine(dir, "pac");
        File.WriteAllText(pac,
            "#!/bin/sh\n" +
            "if [ \"$1\" = \"help\" ]; then echo 'Version: 1.99.0'; exit 0; fi\n" +
            "while [ \"$#\" -gt 0 ]; do\n" +
            "  if [ \"$1\" = \"--sources\" ]; then shift; dest=\"$1\"; fi\n" +
            "  shift\n" +
            "done\n" +
            "mkdir -p \"$dest/Src\"\n" +
            "printf 'App:\\n  Properties:\\n    OnStart: =true\\n' > \"$dest/Src/App.pa.yaml\"\n");
        File.SetUnixFileMode(pac, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var msapp = Path.Combine(dir, "input.msapp");
        File.WriteAllText(msapp, "fixture");
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        string? unpacked = null;
        try
        {
            Environment.SetEnvironmentVariable("PATH", dir + Path.PathSeparator + originalPath);
            var prepared = SourcePreparer.Prepare(msapp, keepTemp: false);
            unpacked = prepared.Folder;
            Assert.True(File.Exists(Path.Combine(unpacked, "Src", "App.pa.yaml")));
            prepared.Dispose();
            Assert.False(Directory.Exists(unpacked));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            if (unpacked is not null && Directory.Exists(unpacked)) Directory.Delete(unpacked, true);
            Directory.Delete(dir, true);
        }
    }
}
