using System.Text.Json;
using Easel.Core.Model;

namespace Easel.Output;

/// <summary>SARIF 2.1.0 output for GitHub code scanning / Azure DevOps.</summary>
public sealed class SarifReportRenderer
{
    private const string InformationUri = "https://github.com/lukoplt/easel";

    public string Render(LintReport report)
    {
        var rules = report.Findings
            .GroupBy(f => f.RuleId)
            .Select(g => g.First())
            .OrderBy(f => f.RuleId, StringComparer.Ordinal)
            .Select(f => new
            {
                id = f.RuleId,
                name = f.RuleName,
                shortDescription = new { text = f.RuleName },
                defaultConfiguration = new { level = Level(f.Severity) },
                properties = new { category = f.Category.ToString() },
            })
            .ToList();

        var results = report.Findings.Select(f => new
        {
            ruleId = f.RuleId,
            level = Level(f.Severity),
            message = new { text = f.Message },
            locations = new[]
            {
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new { uri = Uri(f) },
                        region = f.Location.Line > 0
                            ? new { startLine = f.Location.Line, startColumn = Math.Max(1, f.Location.Column) }
                            : null,
                    },
                },
            },
            partialFingerprints = new Dictionary<string, string> { ["easel/v1"] = f.Fingerprint() },
        }).ToList();

        var sarif = new
        {
            schema = "https://json.schemastore.org/sarif-2.1.0.json",
            version = "2.1.0",
            runs = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = report.Tool,
                            version = report.Version,
                            informationUri = InformationUri,
                            rules,
                        },
                    },
                    results,
                },
            },
        };

        var json = JsonSerializer.Serialize(sarif, new JsonSerializerOptions { WriteIndented = true });
        // The anonymous property "schema" must be emitted as "$schema".
        return json.Replace("\"schema\":", "\"$schema\":");
    }

    private static string Uri(Finding f) =>
        f.Location.IsKnown ? f.Location.File.Replace('\\', '/') : "app";

    private static string Level(Severity s) => s switch
    {
        Severity.Error => "error",
        Severity.Warning => "warning",
        _ => "note",
    };
}
