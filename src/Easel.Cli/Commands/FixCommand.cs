using System.CommandLine;
using Easel.Cli.Ai;
using Easel.Cli.Infrastructure;
using Easel.Core;
using Easel.Core.Config;
using Easel.Core.Model;
using Easel.Fx;
using Easel.Rules;
using Spectre.Console;

namespace Easel.Cli.Commands;

/// <summary>
/// `easel fix --suggest` (opt-in AI) — propose a corrected formula as a diff.
/// Never auto-applies. Any AI suggestion is validated by re-parsing with Power Fx.
/// </summary>
public static class FixCommand
{
    public static Command Build()
    {
        var path = new Argument<string>("path") { Description = "Unpacked source folder or .msapp file." };
        var rule = new Option<string>("--rule") { Description = "Rule id to fix (e.g. PF0001).", Required = true };
        var send = new Option<bool>("--send") { Description = "Call the provider (default: dry run, no network)." };
        var provider = new Option<string>("--provider") { Description = "Provider name.", DefaultValueFactory = _ => "openai-compatible" };
        var endpoint = new Option<string?>("--endpoint") { Description = "Chat-completions endpoint URL." };
        var model = new Option<string>("--model") { Description = "Model id.", DefaultValueFactory = _ => "gpt-4o-mini" };
        var apiKeyEnv = new Option<string>("--api-key-env") { Description = "Env var holding the API key.", DefaultValueFactory = _ => "EASEL_AI_KEY" };

        var cmd = new Command("fix", "Suggest a formula fix for a finding (opt-in AI; never auto-applies).")
        {
            path, rule, send, provider, endpoint, model, apiKeyEnv,
        };
        cmd.SetAction(async (pr, ct) => await Run(
            pr.GetValue(path)!, pr.GetValue(rule)!,
            new AiOptions(pr.GetValue(provider)!, pr.GetValue(endpoint), pr.GetValue(model)!, pr.GetValue(apiKeyEnv)!, pr.GetValue(send)),
            ct));
        return cmd;
    }

    private static async Task<int> Run(string path, string ruleId, AiOptions ai, CancellationToken ct)
    {
        return await CommandRunner.GuardedAsync(async () =>
        {
            using var src = SourcePreparer.Prepare(path, keepTemp: false);
            var analysis = AppAnalysis.FromFolder(src.Folder);
            var findings = RuleEngine.CreateDefault().Run(analysis, ConfigLoader.LoadNearest(src.Folder));

            var finding = findings.FirstOrDefault(f => string.Equals(f.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
            if (finding?.ElementPath is null)
            {
                AnsiConsole.MarkupLine($"[yellow]No '{Markup.Escape(ruleId)}' finding with a formula to fix.[/]");
                return ExitCode.Ok;
            }

            var prop = analysis.Model.AllProperties()
                .FirstOrDefault(p => string.Equals(p.Path, finding.ElementPath, StringComparison.OrdinalIgnoreCase));
            var formula = prop?.Property.Formula;
            if (formula is null)
            {
                AnsiConsole.MarkupLine("[yellow]Could not locate the formula for that finding.[/]");
                return ExitCode.Ok;
            }

            const string system =
                "You are a Power Apps expert. Return ONLY a corrected Power Fx formula (no prose, no code fences) " +
                "that resolves the described issue while preserving intent.";
            var user = $"Issue: {finding.Message}\nRule: {finding.RuleId}\n\nCurrent formula:\n{formula}";

            var provider = LlmProviderFactory.Create(ai);
            AnsiConsole.MarkupLine($"[grey]provider: {provider.Name}[/]");
            var suggestion = (await provider.CompleteAsync(system, user, ct)).Trim();

            if (!ai.Send)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine(suggestion);
                return ExitCode.Ok;
            }

            // Validate the suggestion by re-parsing before showing it as a candidate.
            var parse = analysis.Fx.Parse(suggestion.TrimStart('='));
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Suggested formula[/] (not applied):");
            AnsiConsole.MarkupLine($"[red]- {Markup.Escape(formula)}[/]");
            AnsiConsole.MarkupLine($"[green]+ {Markup.Escape(suggestion)}[/]");
            AnsiConsole.MarkupLine(parse.IsSuccess
                ? "[green]✓ suggestion parses cleanly[/]"
                : $"[yellow]⚠ suggestion does not parse — discard it:[/] {Markup.Escape(parse.Errors.FirstOrDefault()?.Message ?? "")}");
            return ExitCode.Ok;
        });
    }
}
