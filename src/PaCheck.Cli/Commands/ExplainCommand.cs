using System.CommandLine;
using PaCheck.Cli.Ai;
using PaCheck.Cli.Infrastructure;
using PaCheck.Core;
using PaCheck.Core.Config;
using PaCheck.Core.Model;
using PaCheck.Rules;
using Spectre.Console;

namespace PaCheck.Cli.Commands;

/// <summary>`pacheck explain` (opt-in AI) — explain a finding with its formula context.</summary>
public static class ExplainCommand
{
    public static Command Build()
    {
        var path = new Argument<string>("path") { Description = "Unpacked source folder or .msapp file." };
        var rule = new Option<string>("--rule") { Description = "Rule id to explain (e.g. PA1009).", Required = true };
        var send = new Option<bool>("--send") { Description = "Actually call the configured LLM provider (default: dry run, no network)." };
        var provider = new Option<string>("--provider") { Description = "Provider name.", DefaultValueFactory = _ => "openai-compatible" };
        var endpoint = new Option<string?>("--endpoint") { Description = "Chat-completions endpoint URL." };
        var model = new Option<string>("--model") { Description = "Model id.", DefaultValueFactory = _ => "gpt-4o-mini" };
        var apiKeyEnv = new Option<string>("--api-key-env") { Description = "Env var holding the API key.", DefaultValueFactory = _ => "PACHECK_AI_KEY" };

        var cmd = new Command("explain", "Explain a finding using an LLM (opt-in; nothing is sent without --send).")
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
            var config = ConfigLoader.LoadNearest(src.Folder);
            var findings = RuleEngine.CreateDefault().Run(analysis, config);

            var finding = findings.FirstOrDefault(f => string.Equals(f.RuleId, ruleId, StringComparison.OrdinalIgnoreCase));
            if (finding is null)
            {
                AnsiConsole.MarkupLine($"[yellow]No '{Markup.Escape(ruleId)}' finding to explain.[/]");
                return ExitCode.Ok;
            }

            var formula = FindFormula(analysis.Model, finding.ElementPath);
            var (system, user) = BuildPrompt(finding, formula);

            var provider = LlmProviderFactory.Create(ai);
            AnsiConsole.MarkupLine($"[grey]provider: {provider.Name}[/]");
            var answer = await provider.CompleteAsync(system, user, ct);
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(answer);
            return ExitCode.Ok;
        });
    }

    private static (string System, string User) BuildPrompt(Finding f, string? formula)
    {
        const string system =
            "You are a senior Power Apps engineer. Explain the lint finding concisely and give a concrete fix. " +
            "Do not invent APIs. Keep it under 200 words.";
        var user =
            $"Rule {f.RuleId} ({f.RuleName}), severity {f.Severity}.\n" +
            $"Message: {f.Message}\n" +
            $"Location: {f.Location}\n" +
            (f.Help is not null ? $"Hint: {f.Help}\n" : "") +
            (formula is not null ? $"\nFormula:\n{formula}\n" : "");
        return (system, user);
    }

    private static string? FindFormula(AppModel model, string? elementPath)
    {
        if (elementPath is null) return null;
        return model.AllProperties()
            .FirstOrDefault(p => string.Equals(p.Path, elementPath, StringComparison.OrdinalIgnoreCase))?.Property.Formula;
    }
}
