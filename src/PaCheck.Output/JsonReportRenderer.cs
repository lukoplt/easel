using System.Text.Json;
using System.Text.Json.Serialization;
using PaCheck.Core.Model;

namespace PaCheck.Output;

/// <summary>Stable, versioned JSON output for CI consumption.</summary>
public sealed class JsonReportRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Render(LintReport report)
    {
        var dto = new JsonReport
        {
            SchemaVersion = report.SchemaVersion,
            Tool = report.Tool,
            Version = report.Version,
            Target = report.Target,
            Summary = new JsonSummary
            {
                Error = report.Summary.Error,
                Warning = report.Summary.Warning,
                Info = report.Summary.Info,
                Total = report.Summary.Total,
            },
            Findings = report.Findings.Select(ToDto).ToList(),
        };
        return JsonSerializer.Serialize(dto, Options);
    }

    private static JsonFinding ToDto(Finding f) => new()
    {
        RuleId = f.RuleId,
        RuleName = f.RuleName,
        Category = f.Category.ToString(),
        Severity = f.Severity.ToString().ToLowerInvariant(),
        Message = f.Message,
        File = f.Location.IsKnown ? f.Location.File : null,
        Line = f.Location.Line > 0 ? f.Location.Line : null,
        Column = f.Location.Column > 0 ? f.Location.Column : null,
        ElementPath = f.ElementPath,
        Fingerprint = f.Fingerprint(),
        Help = f.Help,
    };

    private sealed class JsonReport
    {
        [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = "";
        [JsonPropertyName("tool")] public string Tool { get; set; } = "";
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("target")] public string Target { get; set; } = "";
        [JsonPropertyName("summary")] public JsonSummary Summary { get; set; } = new();
        [JsonPropertyName("findings")] public List<JsonFinding> Findings { get; set; } = new();
    }

    private sealed class JsonSummary
    {
        [JsonPropertyName("error")] public int Error { get; set; }
        [JsonPropertyName("warning")] public int Warning { get; set; }
        [JsonPropertyName("info")] public int Info { get; set; }
        [JsonPropertyName("total")] public int Total { get; set; }
    }

    private sealed class JsonFinding
    {
        [JsonPropertyName("ruleId")] public string RuleId { get; set; } = "";
        [JsonPropertyName("ruleName")] public string RuleName { get; set; } = "";
        [JsonPropertyName("category")] public string Category { get; set; } = "";
        [JsonPropertyName("severity")] public string Severity { get; set; } = "";
        [JsonPropertyName("message")] public string Message { get; set; } = "";
        [JsonPropertyName("file")] public string? File { get; set; }
        [JsonPropertyName("line")] public int? Line { get; set; }
        [JsonPropertyName("column")] public int? Column { get; set; }
        [JsonPropertyName("elementPath")] public string? ElementPath { get; set; }
        [JsonPropertyName("fingerprint")] public string Fingerprint { get; set; } = "";
        [JsonPropertyName("help")] public string? Help { get; set; }
    }
}
