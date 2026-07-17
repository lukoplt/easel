using System.Collections.Concurrent;

namespace Easel.Fx;

/// <summary>
/// Parses Power Fx formulas once and caches both the parse and its derived facts.
/// Shared across all commands so no formula is parsed twice. Thread-safe.
/// </summary>
public sealed class FxParseService
{
    private readonly PowerFxParser _parser = new();
    private readonly ConcurrentDictionary<string, FxParse> _parseCache = new();
    private readonly ConcurrentDictionary<string, FxFacts> _factsCache = new();

    public FxParse Parse(string expression) =>
        _parseCache.GetOrAdd(expression ?? string.Empty, e => _parser.Parse(e));

    /// <summary>Parse and collect AST facts. Returns <see cref="FxFacts.Empty"/> if unparsable.</summary>
    public FxFacts Facts(string expression)
    {
        return _factsCache.GetOrAdd(expression ?? string.Empty, e =>
        {
            var p = Parse(e);
            return p is { IsSuccess: true, Root: not null }
                ? FxFactsWalker.Collect(p.Root)
                : FxFacts.Empty;
        });
    }
}
