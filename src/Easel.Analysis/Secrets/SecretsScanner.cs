using Easel.Core;
using Easel.Core.Model;
using Easel.Core.Security;

namespace Easel.Analysis.Secrets;

public sealed record ConnectorInventoryItem(string Name, string? Type);

public sealed record SecretsReport(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<ConnectorInventoryItem> DataSources);

/// <summary>
/// Standalone secrets scan with a connector/data-source inventory (DLP relevance).
/// Reuses the same detectors as the PA2xxx lint rules.
/// </summary>
public static class SecretsScanner
{
    public static SecretsReport Scan(AppAnalysis a, SecretScanOptions? options = null)
    {
        var findings = new List<Finding>();

        // Scan the RAW property value (works for literals and unparsable formulas too —
        // a secret must never be missed just because the surrounding formula didn't parse).
        foreach (var pr in a.Model.AllProperties())
        {
            var value = pr.Property.Formula;
            if (string.IsNullOrEmpty(value)) continue;

            foreach (var m in SecretDetectors.Scan(value, options))
            {
                var (id, name, sev) = Map(m.Kind);
                findings.Add(new Finding(id, name, RuleCategory.Security, sev,
                    $"{m.Description}: {m.Redacted}", pr.Property.Location, pr.Path,
                    "Move secrets to a secure store; never hardcode credentials."));
            }
        }

        var inventory = a.Model.DataSources
            .Select(d => new ConnectorInventoryItem(d.Name, d.Type))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SecretsReport(findings, inventory);
    }

    private static (string Id, string Name, Severity Severity) Map(SecretKind kind) => kind switch
    {
        SecretKind.HighEntropy => ("PA2002", "high-entropy-literal", Severity.Warning),
        SecretKind.UrlCredentials => ("PA2003", "url-with-credentials", Severity.Error),
        _ => ("PA2001", "hardcoded-secret", Severity.Error),
    };
}
