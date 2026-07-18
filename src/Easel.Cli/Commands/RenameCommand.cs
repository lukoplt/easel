using System.CommandLine;
using Easel.Analysis.Rename;
using Easel.Cli.Infrastructure;
using Easel.Core;
using Easel.Pac;
using Spectre.Console;

namespace Easel.Cli.Commands;

/// <summary>`easel rename` (preview) — rename a symbol inside a .msapp via pac round-trip.</summary>
public static class RenameCommand
{
    public static Command Build()
    {
        var msapp = new Argument<string>("msapp") { Description = "Input .msapp file (required — Git-integration folders are read-only)." };
        var from = new Option<string>("--from") { Description = "Existing symbol name.", Required = true };
        var to = new Option<string>("--to") { Description = "New symbol name.", Required = true };
        var output = new Option<string?>("--output", "-o") { Description = "Output .msapp path (default: <name>.renamed.msapp beside input)." };
        var keepTemp = new Option<bool>("--keep-temp") { Description = "Keep the temp unpack folder." };

        var cmd = new Command("rename", "Rename a symbol and repack the app (preview).") { msapp, from, to, output, keepTemp };
        cmd.SetAction(pr => Run(pr.GetValue(msapp)!, pr.GetValue(from)!, pr.GetValue(to)!, pr.GetValue(output), pr.GetValue(keepTemp)));
        return cmd;
    }

    private static int Run(string input, string from, string to, string? output, bool keepTemp) =>
        CommandRunner.Guarded(() =>
        {
            var resolved = InputResolver.Resolve(input);
            if (resolved.Kind == InputKind.UnpackedFolder)
                throw new InputException("rename requires a .msapp. A pa.yaml folder from Git integration is read-only — pack it first, or work from the .msapp.");
            if (resolved.Kind != InputKind.Msapp)
                throw new InputException(resolved.Message);

            var pac = PacRunner.Create();
            var temp = PacRunner.TempFolderFor(input) + "-rename";
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);

            AnsiConsole.MarkupLine("[grey]Unpacking via pac…[/]");
            pac.UnpackMsapp(input, temp, line => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]"), AppCancellation.Token);

            try
            {
                var analysis = AppAnalysis.FromFolder(temp);
                var result = RenameEngine.Rename(temp, from, to, analysis);
                if (!result.Success)
                {
                    AnsiConsole.MarkupLine($"[red]{Markup.Escape(result.Message)}[/]");
                    return ExitCode.InputError;
                }
                if (result.StringLiteralHits > 0)
                    AnsiConsole.MarkupLine(
                        $"[yellow]⚠ '{Markup.Escape(from)}' also appears in {result.StringLiteralHits} string literal(s) " +
                        "— these were changed too. Review them in Studio.[/]");

                var outPath = output ?? DefaultOutput(input);
                AnsiConsole.MarkupLine("[grey]Packing via pac…[/]");
                pac.PackMsapp(temp, outPath, line => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]"), AppCancellation.Token);

                AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(result.Message)} " +
                    $"({result.Occurrences} occurrence(s) in {result.FilesChanged} file(s))");
                AnsiConsole.MarkupLine($"[green]→[/] {Markup.Escape(outPath)}");
                AnsiConsole.MarkupLine("[yellow]preview:[/] open the new .msapp in Power Apps Studio and verify before shipping.");
                return ExitCode.Ok;
            }
            finally
            {
                if (!keepTemp && Directory.Exists(temp))
                    try { Directory.Delete(temp, recursive: true); } catch { /* best effort */ }
            }
        });

    private static string DefaultOutput(string input)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(input)) ?? ".";
        var name = Path.GetFileNameWithoutExtension(input);
        return Path.Combine(dir, $"{name}.renamed.msapp");
    }
}
