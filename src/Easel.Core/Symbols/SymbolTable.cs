using Easel.Core.Model;

namespace Easel.Core.Symbols;

public enum SymbolKind
{
    GlobalVariable,
    ContextVariable,
    Collection,
    NamedFormula,
    Control,
    Screen,
    DataSource,
    Media,
}

/// <summary>Where a symbol is defined. A symbol name may have several definitions.</summary>
public sealed record SymbolDefinition(
    string Name,
    SymbolKind Kind,
    string? Scope,
    SourceLocation Location,
    string DefinedInPath);

/// <summary>A read occurrence of a bare identifier.</summary>
public sealed record SymbolReference(
    string Name,
    SourceLocation Location,
    string InPath);

/// <summary>
/// Definitions and reads across the whole app. Built once, queried by rules and analysis.
/// </summary>
public sealed class SymbolTable
{
    private readonly Dictionary<string, List<SymbolDefinition>> _defsByName;
    private readonly Dictionary<string, List<SymbolReference>> _refsByName;

    public IReadOnlyList<SymbolDefinition> Definitions { get; }
    public IReadOnlyList<SymbolReference> References { get; }

    public SymbolTable(IReadOnlyList<SymbolDefinition> definitions, IReadOnlyList<SymbolReference> references)
    {
        Definitions = definitions;
        References = references;
        _defsByName = definitions
            .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        _refsByName = references
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<SymbolDefinition> DefinitionsOf(string name) =>
        _defsByName.TryGetValue(name, out var d) ? d : Array.Empty<SymbolDefinition>();

    public IReadOnlyList<SymbolReference> Usages(string name) =>
        _refsByName.TryGetValue(name, out var r) ? r : Array.Empty<SymbolReference>();

    public int ReadCount(string name) => Usages(name).Count;

    public IEnumerable<SymbolDefinition> OfKind(SymbolKind kind) =>
        Definitions.Where(d => d.Kind == kind);

    public bool IsDefined(string name) => _defsByName.ContainsKey(name);
}
