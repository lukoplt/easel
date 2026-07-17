using System.Reflection;
using PaCheck.Core;
using PaCheck.Core.Config;
using PaCheck.Core.Model;

namespace PaCheck.Rules;

/// <summary>
/// Discovers rules by reflection, runs the enabled ones against an analysis, applies
/// per-rule severity overrides and <c>ignore</c> globs, and returns sorted findings.
/// </summary>
public sealed class RuleEngine
{
    private readonly IReadOnlyList<IRule> _rules;

    public RuleEngine(IReadOnlyList<IRule> rules) => _rules = rules;

    public IReadOnlyList<IRule> Rules => _rules;

    /// <summary>All concrete <see cref="IRule"/> types in this assembly, instantiated.</summary>
    public static RuleEngine CreateDefault()
    {
        var rules = typeof(RuleEngine).Assembly
            .GetTypes()
            .Where(t => typeof(IRule).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IRule)Activator.CreateInstance(t)!)
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
        return new RuleEngine(rules);
    }

    public IReadOnlyList<Finding> Run(AppAnalysis analysis, PaCheckConfig config)
    {
        var findings = new List<Finding>();

        foreach (var rule in _rules)
        {
            var cfg = config.ForRule(rule.Id, rule.Name);
            if (!cfg.Enabled) continue;

            var ctx = new RuleContext(analysis, cfg.Options);
            IEnumerable<Finding> produced;
            try
            {
                produced = rule.Evaluate(ctx).ToList();
            }
            catch (Exception ex)
            {
                // A misbehaving rule must not abort the whole run.
                produced = new[]
                {
                    new Finding(rule.Id, rule.Name, rule.Category, Severity.Info,
                        $"rule threw and was skipped: {ex.Message}", SourceLocation.Unknown),
                };
            }

            foreach (var f in produced)
            {
                var final = cfg.SeverityOverride is { } s ? f with { Severity = s } : f;
                if (!IsIgnored(final, config.Ignore))
                    findings.Add(final);
            }
        }

        return findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Location.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Location.Line)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsIgnored(Finding f, IReadOnlyList<string> ignore)
    {
        if (ignore.Count == 0 || !f.Location.IsKnown) return false;
        return ignore.Any(g => Glob.IsMatch(f.Location.File, g));
    }
}
