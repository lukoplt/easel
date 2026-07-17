using System.CommandLine;
using Easel.Cli.Infrastructure;
using Easel.Core;
using Easel.Core.Baseline;
using Easel.Core.Config;
using Easel.Core.Model;
using Easel.Output;
using Easel.Rules;
using Spectre.Console;

namespace Easel.Cli.Commands;

/// <summary>`easel lint` — run rules and report findings.</summary>
public static class LintCommand
{
    public static Command Build()
    {
        var path = new Argument<string>("path") { Description = "Unpacked source folder or .msapp file." };
        var format = new Option<string>("--format", "-f")
        {
            Description = "Output format: console | json | sarif | html.",
            DefaultValueFactory = _ => "console",
        };
        var failOn = new Option<string>("--fail-on")
        {
            Description = "Minimum severity that fails the run: info | warning | error.",
            DefaultValueFactory = _ => "warning",
        };
        var config = new Option<string?>("--config") { Description = "Path to .easel.yml (default: search upward)." };
        var output = new Option<string?>("--output", "-o") { Description = "Write machine output to a file instead of stdout." };
        var writeBaseline = new Option<bool>("--write-baseline") { Description = "Record current findings as the baseline and exit." };
        var baseline = new Option<string?>("--baseline") { Description = "Baseline file to compare against (default .easel-baseline.json)." };
        var keepTemp = new Option<bool>("--keep-temp") { Description = "Keep the temp unpack folder for debugging." };

        var cmd = new Command("lint", "Analyse an app and report rule findings.")
        {
            path, format, failOn, config, output, writeBaseline, baseline, keepTemp,
        };

        cmd.SetAction(pr => Run(
            pr.GetValue(path)!,
            pr.GetValue(format)!,
            pr.GetValue(failOn)!,
            pr.GetValue(config),
            pr.GetValue(output),
            pr.GetValue(writeBaseline),
            pr.GetValue(baseline),
            pr.GetValue(keepTemp)));

        return cmd;
    }

    private static int Run(string path, string format, string failOn, string? configPath,
        string? outputPath, bool writeBaseline, string? baselinePath, bool keepTemp)
    {
        return CommandRunner.Guarded(() =>
        {
            using var src = SourcePreparer.Prepare(path, keepTemp, msg => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(msg)}[/]"));

            var config = configPath is not null ? ConfigLoader.Load(configPath) : ConfigLoader.LoadNearest(src.Folder);
            var analysis = AppAnalysis.FromFolder(src.Folder);
            var engine = RuleEngine.CreateDefault();
            IReadOnlyList<Finding> findings = analysis.LoadFindings()
                .Concat(engine.Run(analysis, config))
                .OrderByDescending(f => f.Severity)
                .ThenBy(f => f.Location.File, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Location.Line)
                .ToList();

            var baseFile = baselinePath ?? Path.Combine(src.Folder, Baseline.DefaultFileName);
            if (writeBaseline)
            {
                Baseline.FromFindings(findings).Save(baseFile);
                AnsiConsole.MarkupLine($"[green]Baseline written[/] ({findings.Count} findings) → {Markup.Escape(baseFile)}");
                return ExitCode.Ok;
            }
            if (File.Exists(baseFile))
            {
                var before = findings.Count;
                findings = Baseline.Load(baseFile).Filter(findings);
                if (findings.Count != before)
                    AnsiConsole.MarkupLine($"[grey]baseline: {before - findings.Count} known finding(s) suppressed[/]");
            }

            var report = new LintReport(ToolInfo.ToolName, ToolInfo.Version, "1.0", src.OriginalPath, findings);
            Render(report, format, outputPath);

            var threshold = ParseSeverity(failOn);
            var gate = findings.Any(f => f.Severity >= threshold);
            return gate ? ExitCode.FindingsOverThreshold : ExitCode.Ok;
        });
    }

    private static void Render(LintReport report, string format, string? outputPath)
    {
        switch (format.ToLowerInvariant())
        {
            case "json":
                Emit(new JsonReportRenderer().Render(report), outputPath);
                break;
            case "sarif":
                Emit(new SarifReportRenderer().Render(report), outputPath);
                break;
            case "html":
                Emit(new HtmlReportRenderer().Render(report), outputPath);
                break;
            default:
                new ConsoleReportRenderer().Render(report);
                break;
        }
    }

    private static void Emit(string content, string? outputPath)
    {
        if (outputPath is not null)
        {
            File.WriteAllText(outputPath, content);
            AnsiConsole.MarkupLine($"[grey]written → {Markup.Escape(outputPath)}[/]");
        }
        else
        {
            Console.WriteLine(content);
        }
    }

    private static Severity ParseSeverity(string s) => s.Trim().ToLowerInvariant() switch
    {
        "info" => Severity.Info,
        "error" => Severity.Error,
        _ => Severity.Warning,
    };
}
