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

    /// <summary>
    /// Detect secrets in a value. Set <paramref name="includeEntropy"/> false when scanning a
    /// raw formula/expression (not a string literal) — the generic high-entropy heuristic
    /// otherwise fires on identifier chains like <c>DropdownCity_2.SelectedText.Value</c>.
    /// The allowlist suppresses only the specific match it covers, never the whole value.
    /// </summary>
    public static IReadOnlyList<SecretMatch> Scan(string value, SecretScanOptions? options = null, bool includeEntropy = true)
    {
        var opts = options ?? SecretScanOptions.Default;
        var results = new List<SecretMatch>();
        if (string.IsNullOrWhiteSpace(value)) return results;

        bool Allowed(string raw) => opts.Allowlist
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Any(a => raw.Contains(a, StringComparison.OrdinalIgnoreCase));

        void Add(SecretKind kind, string desc, string raw)
        {
            if (!Allowed(raw)) results.Add(new SecretMatch(kind, desc, Redact(raw)));
        }

        foreach (Match match in UrlCredentialsRx().Matches(value))
            Add(SecretKind.UrlCredentials, "URL contains embedded credentials", match.Value);
        foreach (Match match in ConnectionStringRx().Matches(value))
            Add(SecretKind.ConnectionString, "Looks like a connection string with a secret", match.Value);
        foreach (Match match in ApiKeyRx().Matches(value))
            Add(SecretKind.ApiKey, "Looks like an API key", match.Value);
        foreach (Match match in TokenRx().Matches(value))
            Add(SecretKind.Token, "Looks like an access token", match.Value);

        if (includeEntropy)
        {
            var token = LongestToken(value);
            if (token.Length >= opts.MinEntropyLength && LooksRandom(token) &&
                ShannonEntropy(token) >= opts.EntropyThreshold)
                Add(SecretKind.HighEntropy, $"High-entropy string (entropy {ShannonEntropy(token):0.0})", token);
        }

        return results;
    }

    /// <summary>
    /// Scan a property: full detection (incl. entropy) on its actual string literals, plus
    /// pattern-only detection on the raw value so secrets in unparsable formulas or non-quoted
    /// values are still caught — without the entropy heuristic firing on expressions. Deduped.
    /// </summary>
    public static IReadOnlyList<SecretMatch> ScanProperty(
        string rawValue,
        IEnumerable<string> stringLiterals,
        bool isFormula,
        bool parseSucceeded,
        SecretScanOptions? options = null)
    {
        var opts = options ?? SecretScanOptions.Default;
        var seen = new HashSet<(SecretKind, string)>();
        var results = new List<SecretMatch>();

        void Take(IEnumerable<SecretMatch> matches)
        {
            foreach (var m in matches)
                if (seen.Add((m.Kind, m.Redacted)))
                    results.Add(m);
        }

        if (!isFormula)
        {
            Take(Scan(rawValue, opts, includeEntropy: true));
            return results;
        }

        // Valid formulas are trusted structurally: only their actual string literals are data.
        // Scanning the whole expression would mistake identifiers such as api_key for secrets.
        foreach (var literal in stringLiterals)
            Take(Scan(literal, opts, includeEntropy: true));

        if (!parseSucceeded)
        {
            // A broken formula has no reliable AST. Recover quoted strings lexically so generic
            // entropy detection still works, then use pattern-only raw fallback for unquoted
            // high-confidence formats such as AKIA... and JWTs.
            foreach (var literal in ExtractQuotedStrings(rawValue))
                Take(Scan(literal, opts, includeEntropy: true));
            Take(Scan(rawValue, opts, includeEntropy: false));
        }

        return results;
    }

    private static IEnumerable<string> ExtractQuotedStrings(string expression)
    {
        for (var i = 0; i < expression.Length; i++)
        {
            if (expression[i] != '"') continue;
            var value = new System.Text.StringBuilder();
            i++;
            while (i < expression.Length)
            {
                if (expression[i] == '"')
                {
                    if (i + 1 < expression.Length && expression[i + 1] == '"')
                    {
                        value.Append('"');
                        i += 2;
                        continue;
                    }
                    break;
                }
                value.Append(expression[i++]);
            }
            if (value.Length > 0) yield return value.ToString();
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
