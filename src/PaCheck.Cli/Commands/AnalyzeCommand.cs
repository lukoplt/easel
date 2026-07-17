using System.CommandLine;
using PaCheck.Analysis.Analyze;
using PaCheck.Cli.Infrastructure;
using PaCheck.Core;
using Spectre.Console;

namespace PaCheck.Cli.Commands;

/// <summary>`pacheck analyze` — find-usages / dead-code / impact / graph.</summary>
public static class AnalyzeCommand
{
    public static Command Build()
    {
        var path = new Argument<string>("path") { Description = "Unpacked source folder or .msapp file." };
        var find = new Option<string?>("--find") { Description = "Show the definition and all usages of a symbol." };
        var deadCode = new Option<bool>("--dead-code") { Description = "List unused variables/collections/media and unreachable screens." };
        var impact = new Option<string?>("--impact") { Description = "Show the transitive impact of a symbol." };
        var graph = new Option<string?>("--graph") { Description = "Export the dependency graph: mermaid | dot." };
        var output = new Option<string?>("--output", "-o") { Description = "Write graph output to a file." };
        var keepTemp = new Option<bool>("--keep-temp") { Description = "Keep the temp unpack folder." };

        var cmd = new Command("analyze", "Query symbols, dead code, impact and the dependency graph.")
        {
            path, find, deadCode, impact, graph, output, keepTemp,
        };
        cmd.SetAction(pr => Run(pr.GetValue(path)!, pr.GetValue(find), pr.GetValue(deadCode),
            pr.GetValue(impact), pr.GetValue(graph), pr.GetValue(output), pr.GetValue(keepTemp)));
        return cmd;
    }

    private static int Run(string path, string? find, bool deadCode, string? impact, string? graph,
        string? outputPath, bool keepTemp) =>
        CommandRunner.Guarded(() =>
        {
            using var src = SourcePreparer.Prepare(path, keepTemp, m => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(m)}[/]"));
            var a = AppAnalysis.FromFolder(src.Folder);

            if (find is not null) return DoFind(a, find);
            if (impact is not null) return DoImpact(a, impact);
            if (graph is not null) return DoGraph(a, graph, outputPath);
            if (deadCode) return DoDeadCode(a);

            AnsiConsole.MarkupLine("[yellow]Specify one of --find, --dead-code, --impact or --graph.[/]");
            return ExitCode.Ok;
        });

    private static int DoFind(AppAnalysis a, string name)
    {
        var u = AnalyzeEngine.Find(a, name);
        if (u.Definitions.Count == 0 && u.Usages.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]'{Markup.Escape(name)}' not found.[/]");
            return ExitCode.Ok;
        }
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(name)}[/]");
        foreach (var d in u.Definitions)
            AnsiConsole.MarkupLine($"  [green]def[/] {d.Kind} @ {Markup.Escape(d.Location.ToString())} ({Markup.Escape(d.DefinedInPath)})");
        foreach (var r in u.Usages)
            AnsiConsole.MarkupLine($"  [blue]use[/] {Markup.Escape(r.Location.ToString())} in {Markup.Escape(r.InPath)}");
        AnsiConsole.MarkupLine($"[grey]{u.Definitions.Count} definition(s), {u.Usages.Count} usage(s)[/]");
        return ExitCode.Ok;
    }

    private static int DoImpact(AppAnalysis a, string name)
    {
        var nodes = AnalyzeEngine.Impact(a, name);
        if (nodes.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]'{Markup.Escape(name)}' has no dependents (or is not defined).[/]");
            return ExitCode.Ok;
        }
        AnsiConsole.MarkupLine($"[bold]Impact of {Markup.Escape(name)}[/] — {nodes.Count} dependent element(s):");
        foreach (var n in nodes.OrderBy(n => n.Kind).ThenBy(n => n.Name))
            AnsiConsole.MarkupLine($"  {n.Kind}: {Markup.Escape(n.Name)}");
        return ExitCode.Ok;
    }

    private static int DoGraph(AppAnalysis a, string format, string? outputPath)
    {
        var text = AnalyzeEngine.Graph(a, format);
        if (outputPath is not null) { File.WriteAllText(outputPath, text); AnsiConsole.MarkupLine($"[grey]written → {Markup.Escape(outputPath)}[/]"); }
        else Console.WriteLine(text);
        return ExitCode.Ok;
    }

    private static int DoDeadCode(AppAnalysis a)
    {
        var r = AnalyzeEngine.DeadCode(a);
        if (r.Total == 0) { AnsiConsole.MarkupLine("[green]✓ No dead code found.[/]"); return ExitCode.Ok; }

        void Section(string title, IEnumerable<string> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return;
            AnsiConsole.MarkupLine($"[bold]{title}[/] ({list.Count})");
            foreach (var i in list) AnsiConsole.MarkupLine($"  [yellow]•[/] {Markup.Escape(i)}");
        }
        Section("Unused variables", r.UnusedVariables.Select(d => d.Name));
        Section("Unused collections", r.UnusedCollections.Select(d => d.Name));
        Section("Unused media", r.UnusedMedia.Select(m => m.FileName ?? m.Name));
        Section("Unreachable screens", r.UnreachableScreens);
        AnsiConsole.MarkupLine($"[grey]{r.Total} dead-code item(s)[/]");
        return ExitCode.Ok;
    }
}
