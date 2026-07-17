using System.CommandLine;
using System.Text;
using PaCheck.Analysis.Diff;
using PaCheck.Cli.Infrastructure;
using PaCheck.Core;
using Spectre.Console;

namespace PaCheck.Cli.Commands;

/// <summary>`pacheck diff` — semantic diff of two app versions.</summary>
public static class DiffCommand
{
    public static Command Build()
    {
        var baseArg = new Argument<string>("base") { Description = "Baseline source folder or .msapp." };
        var headArg = new Argument<string>("head") { Description = "Changed source folder or .msapp." };
        var format = new Option<string>("--format", "-f") { Description = "console | markdown | json.", DefaultValueFactory = _ => "console" };
        var output = new Option<string?>("--output", "-o") { Description = "Write output to a file." };
        var keepTemp = new Option<bool>("--keep-temp") { Description = "Keep temp unpack folders." };

        var cmd = new Command("diff", "Compare two app versions and classify changes.") { baseArg, headArg, format, output, keepTemp };
        cmd.SetAction(pr => Run(pr.GetValue(baseArg)!, pr.GetValue(headArg)!, pr.GetValue(format)!, pr.GetValue(output), pr.GetValue(keepTemp)));
        return cmd;
    }

    private static int Run(string basePath, string headPath, string format, string? outputPath, bool keepTemp) =>
        CommandRunner.Guarded(() =>
        {
            using var baseSrc = SourcePreparer.Prepare(basePath, keepTemp);
            using var headSrc = SourcePreparer.Prepare(headPath, keepTemp);

            var baseModel = AppAnalysis.FromFolder(baseSrc.Folder).Model;
            var headModel = AppAnalysis.FromFolder(headSrc.Folder).Model;
            var report = DiffEngine.Diff(baseModel, headModel);

            switch (format.ToLowerInvariant())
            {
                case "markdown": Emit(Markdown(report), outputPath); break;
                case "json": Emit(Json(report), outputPath); break;
                default: RenderConsole(report); break;
            }
            return ExitCode.Ok;
        });

    private static void RenderConsole(DiffReport r)
    {
        if (r.IsEmpty) { AnsiConsole.MarkupLine("[green]✓ No changes.[/]"); return; }
        foreach (var c in r.Changes.OrderBy(c => c.Kind))
        {
            var tag = c.Kind switch
            {
                ChangeKind.Added => "[green]+ added   [/]",
                ChangeKind.Removed => "[red]- removed [/]",
                ChangeKind.Renamed => "[blue]~ renamed [/]",
                ChangeKind.Moved => "[yellow]→ moved   [/]",
                _ => "[yellow]* changed [/]",
            };
            var detail = c.Detail is not null ? $" [grey]({Markup.Escape(c.Detail)})[/]" : "";
            AnsiConsole.MarkupLine($"{tag} {c.ElementKind} [bold]{Markup.Escape(c.Name)}[/]{detail}");
        }
        AnsiConsole.MarkupLine(
            $"[grey]{r.Count(ChangeKind.Added)} added, {r.Count(ChangeKind.Removed)} removed, " +
            $"{r.Count(ChangeKind.Renamed)} renamed, {r.Count(ChangeKind.Moved)} moved, " +
            $"{r.Count(ChangeKind.PropertyChanged)} property change(s)[/]");
    }

    private static string Markdown(DiffReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## pacheck diff");
        sb.AppendLine();
        if (r.IsEmpty) { sb.AppendLine("_No changes._"); return sb.ToString(); }
        sb.AppendLine($"**{r.Count(ChangeKind.Added)}** added · **{r.Count(ChangeKind.Removed)}** removed · " +
                      $"**{r.Count(ChangeKind.Renamed)}** renamed · **{r.Count(ChangeKind.Moved)}** moved · " +
                      $"**{r.Count(ChangeKind.PropertyChanged)}** property changes");
        sb.AppendLine();
        sb.AppendLine("| Change | Element | Name | Detail |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var c in r.Changes.OrderBy(c => c.Kind))
            sb.AppendLine($"| {c.Kind} | {c.ElementKind} | `{c.Name}` | {c.Detail ?? ""} |");
        return sb.ToString();
    }

    private static string Json(DiffReport r) =>
        System.Text.Json.JsonSerializer.Serialize(
            new { summary = new { added = r.Count(ChangeKind.Added), removed = r.Count(ChangeKind.Removed), renamed = r.Count(ChangeKind.Renamed), moved = r.Count(ChangeKind.Moved), propertyChanged = r.Count(ChangeKind.PropertyChanged) }, changes = r.Changes },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });

    private static void Emit(string content, string? outputPath)
    {
        if (outputPath is not null) { File.WriteAllText(outputPath, content); AnsiConsole.MarkupLine($"[grey]written → {Markup.Escape(outputPath)}[/]"); }
        else Console.WriteLine(content);
    }
}
