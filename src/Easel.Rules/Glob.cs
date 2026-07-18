using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Easel.Rules;

/// <summary>Minimal glob matcher supporting <c>*</c>, <c>**</c> and <c>?</c> over '/'-paths.</summary>
public static class Glob
{
    // Thread-safe: rules may run in parallel over the same patterns.
    private static readonly ConcurrentDictionary<string, Regex> Cache = new();

    public static bool IsMatch(string path, string pattern)
    {
        var normPath = path.Replace('\\', '/');
        var rx = Cache.GetOrAdd(pattern, p => new Regex(Translate(p), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        return rx.IsMatch(normPath);
    }

    private static string Translate(string pattern)
    {
        var p = pattern.Replace('\\', '/');
        var sb = new StringBuilder("^");
        for (int i = 0; i < p.Length; i++)
        {
            char c = p[i];
            switch (c)
            {
                case '*':
                    if (i + 1 < p.Length && p[i + 1] == '*')
                    {
                        sb.Append(".*");
                        i++;
                        if (i + 1 < p.Length && p[i + 1] == '/') i++; // consume trailing slash of **/
                    }
                    else sb.Append("[^/]*");
                    break;
                case '?': sb.Append("[^/]"); break;
                case '.':
                case '(':
                case ')':
                case '+':
                case '|':
                case '^':
                case '$':
                case '{':
                case '}':
                case '[':
                case ']':
                case '\\':
                    sb.Append('\\').Append(c); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('$');
        return sb.ToString();
    }
}
