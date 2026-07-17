using System.CommandLine;
using System.Text.Json;
using PaCheck.Analysis.Stats;
using PaCheck.Cli.Infrastructure;
using PaCheck.Core;
using Spectre.Console;

namespace PaCheck.Cli.Commands;

/// <summary>`pacheck stats` — app metrics.</summary>
public static class StatsCommand
{
    public static Command Build()
    {
        var path = new Argument<string>("path") { Description = "Unpacked source folder or .msapp file." };
        var format = new Option<string>("--format", "-f") { Description = "console | json.", DefaultValueFactory = _ => "console" };
        var output = new Option<string?>("--output", "-o") { Description = "Write JSON to a file." };
        var keepTemp = new Option<bool>("--keep-temp") { Description = "Keep the temp unpack folder." };

        var cmd = new Command("stats", "Report metrics (controls, media, complexity).") { path, format, output, keepTemp };
        cmd.SetAction(pr => Run(pr.GetValue(path)!, pr.GetValue(format)!, pr.GetValue(output), pr.GetValue(keepTemp)));
        return cmd;
    }

    private static int Run(string path, string format, string? outputPath, bool keepTemp) =>
        CommandRunner.Guarded(() =>
        {
            using var src = SourcePreparer.Prepare(path, keepTemp, m => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(m)}[/]"));
            var analysis = AppAnalysis.FromFolder(src.Folder);
            var stats = StatsEngine.Compute(analysis);

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                });
                if (outputPath is not null) { File.WriteAllText(outputPath, json); AnsiConsole.MarkupLine($"[grey]written → {Markup.Escape(outputPath)}[/]"); }
                else Console.WriteLine(json);
            }
            else
            {
                RenderConsole(stats, src.OriginalPath);
            }
            return ExitCode.Ok;
        });

    private static void RenderConsole(AppStats s, string target)
    {
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(target)}[/]");
        var t = new Table().RoundedBorder();
        t.AddColumn("Metric");
        t.AddColumn(new TableColumn("Value").RightAligned());
        void Row(string k, object v) => t.AddRow(k, v.ToString() ?? "");
        Row("Screens", s.ScreenCount);
        Row("Controls", s.ControlCount);
        Row("Components", s.ComponentCount);
        Row("Data sources", s.DataSourceCount);
        Row("Media assets", $"{s.MediaCount} ({s.MediaBytes / 1024.0:0.#} KB)");
        Row("Global variables", s.GlobalVariableCount);
        Row("Context variables", s.ContextVariableCount);
        Row("Collections", s.CollectionCount);
        Row("Named formulas", s.NamedFormulaCount);
        Row("Formulas", s.FormulaCount);
        Row("Avg formula complexity", s.AverageFormulaComplexity);
        Row("Max formula complexity", $"{s.MaxFormulaComplexity} ({s.MostComplexFormulaPath ?? "-"})");
        AnsiConsole.Write(t);

        if (s.Screens.Count > 0)
        {
            var st = new Table().RoundedBorder().Title("Per screen");
            st.AddColumn("Screen");
            st.AddColumn(new TableColumn("Controls").RightAligned());
            st.AddColumn(new TableColumn("Max depth").RightAligned());
            foreach (var sc in s.Screens)
                st.AddRow(Markup.Escape(sc.Name), sc.ControlCount.ToString(), sc.MaxControlDepth.ToString());
            AnsiConsole.Write(st);
        }
    }
}
