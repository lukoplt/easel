using Easel.Pac;
using Spectre.Console;

namespace Easel.Cli.Infrastructure;

/// <summary>Wraps a command body, mapping exceptions to the documented exit codes.</summary>
public static class CommandRunner
{
    public static async Task<int> GuardedAsync(Func<Task<int>> body)
    {
        try
        {
            return await body();
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]cancelled[/]");
            return ExitCode.Cancelled;
        }
        catch (InputException ex)
        {
            AnsiConsole.MarkupLine($"[red]input error:[/] {Markup.Escape(ex.Message)}");
            return ExitCode.InputError;
        }
        catch (PacException ex)
        {
            AnsiConsole.MarkupLine($"[red]pac error:[/] {Markup.Escape(ex.Message)}");
            return ExitCode.PacMissingOrIncompatible;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]internal error:[/] {Markup.Escape(ex.Message)}");
            return ExitCode.InternalError;
        }
    }

    public static int Guarded(Func<int> body)
    {
        try
        {
            return body();
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]cancelled[/]");
            return ExitCode.Cancelled;
        }
        catch (InputException ex)
        {
            AnsiConsole.MarkupLine($"[red]input error:[/] {Markup.Escape(ex.Message)}");
            return ExitCode.InputError;
        }
        catch (PacException ex)
        {
            AnsiConsole.MarkupLine($"[red]pac error:[/] {Markup.Escape(ex.Message)}");
            return ExitCode.PacMissingOrIncompatible;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]internal error:[/] {Markup.Escape(ex.Message)}");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return ExitCode.InternalError;
        }
    }
}
