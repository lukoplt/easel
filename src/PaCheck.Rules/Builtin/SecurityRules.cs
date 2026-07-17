using PaCheck.Core.Model;
using PaCheck.Core.Security;

namespace PaCheck.Rules.Builtin;

/// <summary>Shared scanning for the PA2xxx security rules.</summary>
public abstract class SecretRuleBase : RuleBase
{
    public override RuleCategory Category => RuleCategory.Security;
    public override Severity DefaultSeverity => Severity.Error;

    protected abstract SecretKind Kind { get; }

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        var allowlist = ctx.Options.Child("allowlist").AsStringList().ToList();
        var opts = allowlist.Count > 0 ? SecretScanOptions.Default with { Allowlist = allowlist } : SecretScanOptions.Default;

        foreach (var pr in ctx.Formulas())
        {
            foreach (var lit in ctx.Fx.Facts(pr.Property.Formula).Strings)
            {
                foreach (var match in SecretDetectors.Scan(lit.Value, opts))
                {
                    if (match.Kind != Kind) continue;
                    yield return Report(
                        $"{match.Description}: {match.Redacted}",
                        pr.Property.Location, pr.Path,
                        help: "Move secrets to a secure store (Azure Key Vault, environment variables); never hardcode.");
                }
            }
        }
    }
}

/// <summary>PA2001 — suspicious literal (API key / token / connection string).</summary>
public sealed class ApiKeyLiteralRule : SecretRuleBase
{
    public override string Id => "PA2001";
    public override string Name => "hardcoded-secret";
    protected override SecretKind Kind => SecretKind.ApiKey;

    public override IEnumerable<Finding> Evaluate(RuleContext ctx)
    {
        // PA2001 covers connection strings, API keys and tokens together.
        var allowlist = ctx.Options.Child("allowlist").AsStringList().ToList();
        var opts = allowlist.Count > 0 ? SecretScanOptions.Default with { Allowlist = allowlist } : SecretScanOptions.Default;
        var kinds = new[] { SecretKind.ApiKey, SecretKind.Token, SecretKind.ConnectionString };

        foreach (var pr in ctx.Formulas())
            foreach (var lit in ctx.Fx.Facts(pr.Property.Formula).Strings)
                foreach (var match in SecretDetectors.Scan(lit.Value, opts))
                    if (kinds.Contains(match.Kind))
                        yield return Report(
                            $"{match.Description}: {match.Redacted}",
                            pr.Property.Location, pr.Path,
                            help: "Move secrets to a secure store; never hardcode credentials.");
    }
}

/// <summary>PA2002 — high-entropy literal that may be a secret.</summary>
public sealed class HighEntropyRule : SecretRuleBase
{
    public override string Id => "PA2002";
    public override string Name => "high-entropy-literal";
    public override Severity DefaultSeverity => Severity.Warning;
    protected override SecretKind Kind => SecretKind.HighEntropy;
}

/// <summary>PA2003 — URL with embedded credentials.</summary>
public sealed class UrlCredentialsRule : SecretRuleBase
{
    public override string Id => "PA2003";
    public override string Name => "url-with-credentials";
    protected override SecretKind Kind => SecretKind.UrlCredentials;
}
