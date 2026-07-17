using System.Text.RegularExpressions;

namespace Easel.Core.Security;

public enum SecretKind { ConnectionString, ApiKey, Token, UrlCredentials, HighEntropy }

public sealed record SecretMatch(SecretKind Kind, string Description, string Redacted);

/// <summary>Options controlling secret detection (allowlist, entropy threshold).</summary>
public sealed record SecretScanOptions(
    IReadOnlyList<string> Allowlist,
    double EntropyThreshold = 4.2,
    int MinEntropyLength = 20)
{
    public static readonly SecretScanOptions Default = new(Array.Empty<string>());
}

/// <summary>
/// Pattern + entropy detectors for secrets in string literals. Shared by the PA2xxx
/// lint rules and the <c>secrets</c> command. Conservative — favours fewer false positives.
/// </summary>
public static partial class SecretDetectors
{
    [GeneratedRegex(@"(?i)(password|pwd|accountkey|shared\s*access\s*key)\s*=\s*[^;\s]{4,}")]
    private static partial Regex ConnectionStringRx();

    [GeneratedRegex(@"AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z\-_]{35}|(?i)(api[_-]?key|secret|client[_-]?secret)\s*[:=]\s*[A-Za-z0-9\-_]{16,}")]
    private static partial Regex ApiKeyRx();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{4,}|xox[baprs]-[0-9A-Za-z-]{10,}|gh[pousr]_[A-Za-z0-9]{20,}")]
    private static partial Regex TokenRx();

    [GeneratedRegex(@"(?i)https?://[^/\s:@]+:[^/\s:@]+@")]
    private static partial Regex UrlCredentialsRx();

    public static IEnumerable<SecretMatch> Scan(string literal, SecretScanOptions? options = null)
    {
        var opts = options ?? SecretScanOptions.Default;
        if (string.IsNullOrWhiteSpace(literal)) yield break;
        if (opts.Allowlist.Any(a => literal.Contains(a, StringComparison.OrdinalIgnoreCase))) yield break;

        if (UrlCredentialsRx().IsMatch(literal))
            yield return new SecretMatch(SecretKind.UrlCredentials, "URL contains embedded credentials", Redact(literal));
        if (ConnectionStringRx().IsMatch(literal))
            yield return new SecretMatch(SecretKind.ConnectionString, "Looks like a connection string with a secret", Redact(literal));
        if (ApiKeyRx().IsMatch(literal))
            yield return new SecretMatch(SecretKind.ApiKey, "Looks like an API key", Redact(literal));
        if (TokenRx().IsMatch(literal))
            yield return new SecretMatch(SecretKind.Token, "Looks like an access token", Redact(literal));

        // High-entropy generic secret — only if no specific pattern already matched.
        var token = LongestToken(literal);
        if (token.Length >= opts.MinEntropyLength &&
            LooksRandom(token) &&
            ShannonEntropy(token) >= opts.EntropyThreshold)
        {
            yield return new SecretMatch(SecretKind.HighEntropy,
                $"High-entropy string (entropy {ShannonEntropy(token):0.0})", Redact(token));
        }
    }

    public static double ShannonEntropy(string s)
    {
        if (s.Length == 0) return 0;
        var counts = new Dictionary<char, int>();
        foreach (var c in s) counts[c] = counts.GetValueOrDefault(c) + 1;
        double entropy = 0;
        foreach (var n in counts.Values)
        {
            double p = (double)n / s.Length;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    private static string LongestToken(string s) =>
        s.Split(new[] { ' ', '"', '\'', ',', ';', '/', '\\', '(', ')', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
         .OrderByDescending(t => t.Length)
         .FirstOrDefault() ?? "";

    private static bool LooksRandom(string s)
    {
        int alnum = s.Count(char.IsLetterOrDigit);
        return alnum >= s.Length * 0.8 && s.Any(char.IsDigit) && s.Any(char.IsLetter);
    }

    private static string Redact(string s)
    {
        var t = s.Trim().Trim('"');
        if (t.Length <= 6) return "***";
        return $"{t[..2]}…{t[^2..]} (len {t.Length})";
    }
}
