using PaCheck.Core.Model;
using Spectre.Console;

namespace PaCheck.Output;

/// <summary>Human-facing console output: grouped by file, coloured by severity, with a summary.</summary>
public sealed class ConsoleReportRenderer
{
    private readonly IAnsiConsole _console;

    public ConsoleReportRenderer(IAnsiConsole? console = null) =>
        _console = console ?? AnsiConsole.Console;

    public void Render(LintReport report)
    {
        if (report.Findings.Count == 0)
        {
            _console.MarkupLine($"[green]✓[/] No findings in [bold]{Escape(report.Target)}[/].");
            return;
        }

        var byFile = report.Findings
            .GroupBy(f => f.Location.IsKnown ? f.Location.File : "(app)")
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byFile)
        {
            _console.MarkupLine($"[underline bold]{Escape(group.Key)}[/]");
            foreach (var f in group.OrderBy(f => f.Location.Line).ThenBy(f => f.RuleId, StringComparer.Ordinal))
            {
                var pos = f.Location.Line > 0 ? $"{f.Location.Line}:{f.Location.Column}" : "-";
                _console.MarkupLine(
                    $"  [grey]{pos,-7}[/] {SeverityTag(f.Severity)} [bold]{f.RuleId}[/] {Escape(f.Message)}");
                if (!string.IsNullOrEmpty(f.Help))
                    _console.MarkupLine($"          [grey]{Escape(f.Help!)}[/]");
            }
            _console.WriteLine();
        }

        RenderSummary(report.Summary);
    }

    private void RenderSummary(ReportSummary s)
    {
        var parts = new List<string>();
        if (s.Error > 0) parts.Add($"[red]{s.Error} error{Plural(s.Error)}[/]");
        if (s.Warning > 0) parts.Add($"[yellow]{s.Warning} warning{Plural(s.Warning)}[/]");
        if (s.Info > 0) parts.Add($"[blue]{s.Info} info[/]");
        var summary = parts.Count > 0 ? string.Join(", ", parts) : "[green]clean[/]";
        _console.MarkupLine($"[bold]{s.Total}[/] finding{Plural(s.Total)} — {summary}");
    }

    private static string SeverityTag(Severity s) => s switch
    {
        Severity.Error => "[red]error  [/]",
        Severity.Warning => "[yellow]warning[/]",
        _ => "[blue]info   [/]",
    };

    private static string Plural(int n) => n == 1 ? "" : "s";
    private static string Escape(string s) => Markup.Escape(s);
}
