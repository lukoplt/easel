namespace Easel.Core.Model;

/// <summary>
/// A single property on a control/screen/app — a name plus its Power Fx formula.
/// The leading '=' from the YAML source is stripped; <see cref="IsFormula"/> records
/// whether the source value actually started with '=' (vs a literal scalar).
/// </summary>
public sealed record Property(
    string Name,
    string Formula,
    bool IsFormula,
    SourceLocation Location)
{
    public bool HasFormula => !string.IsNullOrWhiteSpace(Formula);
}

/// <summary>
/// A control instance. Controls nest arbitrarily deep via <see cref="Children"/>.
/// <see cref="Version"/> preserves the <c>Control@version</c> suffix from the source.
/// </summary>
public sealed record Control(
    string Name,
    string ControlType,
    string? Version,
    string? Variant,
    IReadOnlyList<Property> Properties,
    IReadOnlyList<Control> Children,
    SourceLocation Location)
{
    /// <summary>Depth-first enumeration of this control and all descendants.</summary>
    public IEnumerable<Control> SelfAndDescendants()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var c in child.SelfAndDescendants())
                yield return c;
    }

    public Property? GetProperty(string name) =>
        Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A screen and its control tree.</summary>
public sealed record Screen(
    string Name,
    IReadOnlyList<Property> Properties,
    IReadOnlyList<Control> Children,
    SourceLocation Location)
{
    /// <summary>All controls on this screen, flattened depth-first.</summary>
    public IEnumerable<Control> AllControls() =>
        Children.SelectMany(c => c.SelfAndDescendants());

    public Property? GetProperty(string name) =>
        Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The App object: global properties, OnStart, and named formulas.</summary>
public sealed record AppInfo(
    string Name,
    IReadOnlyList<Property> Properties,
    IReadOnlyList<NamedFormula> Formulas,
    SourceLocation Location)
{
    public Property? GetProperty(string name) =>
        Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A canvas component definition (reusable control assembly).</summary>
public sealed record ComponentDefinition(
    string Name,
    IReadOnlyList<Property> Properties,
    IReadOnlyList<Control> Children,
    SourceLocation Location)
{
    public IEnumerable<Control> AllControls() =>
        Children.SelectMany(c => c.SelfAndDescendants());
}

/// <summary>A named formula (App.Formulas or component formula).</summary>
public sealed record NamedFormula(
    string Name,
    string Formula,
    SourceLocation Location);

/// <summary>A data source / connector reference the app binds to.</summary>
public sealed record DataSource(
    string Name,
    string? Type,
    string? ConnectorId,
    SourceLocation Location);

/// <summary>A media asset (image, video, audio) embedded in the app.</summary>
public sealed record MediaAsset(
    string Name,
    string? FileName,
    string? Kind,
    long? SizeBytes,
    SourceLocation Location);
