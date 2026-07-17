using System.CommandLine;
using System.Text.Json;
using PaCheck.Analysis.Secrets;
using PaCheck.Cli.Infrastructure;
using PaCheck.Core;
using PaCheck.Core.Config;
using PaCheck.Core.Security;
using PaCheck.Output;
using Spectre.Console;

namespace PaCheck.Cli.Commands;

/// <summary>`pacheck secrets` — secret scan plus connector inventory.</summary>
public static class SecretsCommand
{
    public static Command Build()
    {
        var path = new Argument<string>("path") { Description = "Unpacked source folder or .msapp file." };
        var format = new Option<string>("--format", "-f") { Description = "console | json.", DefaultValueFactory = _ => "console" };
        var config = new Option<string?>("--config") { Description = "Path to .pacheck.yml (for the allowlist)." };
        var output = new Option<string?>("--output", "-o") { Description = "Write JSON to a file." };
        var failOn = new Option<bool>("--fail-on-secret") { Description = "Exit 1 if any secret is found." };
        var keepTemp = new Option<bool>("--keep-temp") { Description = "Keep the temp unpack folder." };

        var cmd = new Command("secrets", "Scan for hardcoded secrets and inventory data sources.")
        {
            path, format, config, output, failOn, keepTemp,
        };
        cmd.SetAction(pr => Run(pr.GetValue(path)!, pr.GetValue(format)!, pr.GetValue(config),
            pr.GetValue(output), pr.GetValue(failOn), pr.GetValue(keepTemp)));
        return cmd;
    }

    private static int Run(string path, string format, string? configPath, string? outputPath, bool failOnSecret, bool keepTemp) =>
        CommandRunner.Guarded(() =>
        {
            using var src = SourcePreparer.Prepare(path, keepTemp, m => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(m)}[/]"));
            var cfg = configPath is not null ? ConfigLoader.Load(configPath) : ConfigLoader.LoadNearest(src.Folder);
            var allowlist = cfg.ForRule("PA2001", "hardcoded-secret").Options.Child("allowlist").AsStringList().ToList();
            var opts = allowlist.Count > 0 ? SecretScanOptions.Default with { Allowlist = allowlist } : SecretScanOptions.Default;

            var analysis = AppAnalysis.FromFolder(src.Folder);
            var report = SecretsScanner.Scan(analysis, opts);

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var dto = new
                {
                    secrets = report.Findings.Select(f => new { f.RuleId, f.Severity, f.Message, file = f.Location.File, line = f.Location.Line, path = f.ElementPath }),
                    dataSources = report.DataSources,
                };
                var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                if (outputPath is not null) { File.WriteAllText(outputPath, json); AnsiConsole.MarkupLine($"[grey]written → {Markup.Escape(outputPath)}[/]"); }
                else Console.WriteLine(json);
            }
            else
            {
                var lint = new LintReport(ToolInfo.ToolName, ToolInfo.Version, "1.0", src.OriginalPath, report.Findings);
                new ConsoleReportRenderer().Render(lint);
                RenderInventory(report);
            }

            return failOnSecret && report.Findings.Count > 0 ? ExitCode.FindingsOverThreshold : ExitCode.Ok;
        });

    private static void RenderInventory(SecretsReport report)
    {
        if (report.DataSources.Count == 0) return;
        AnsiConsole.WriteLine();
        var t = new Table().RoundedBorder().Title("Data source inventory (DLP)");
        t.AddColumn("Name");
        t.AddColumn("Type");
        foreach (var d in report.DataSources)
            t.AddRow(Markup.Escape(d.Name), Markup.Escape(d.Type ?? "-"));
        AnsiConsole.Write(t);
    }
}
