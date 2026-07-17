namespace Easel.Core.Model;

/// <summary>
/// Immutable, YAML-independent model of a canvas app. Built once and shared across
/// every command so nothing parses twice.
/// </summary>
public sealed record AppModel(
    AppInfo App,
    IReadOnlyList<Screen> Screens,
    IReadOnlyList<ComponentDefinition> Components,
    IReadOnlyList<DataSource> DataSources,
    IReadOnlyList<MediaAsset> Media,
    string RootPath)
{
    /// <summary>Every screen control, flattened.</summary>
    public IEnumerable<Control> AllControls() =>
        Screens.SelectMany(s => s.AllControls())
               .Concat(Components.SelectMany(c => c.AllControls()));

    /// <summary>Every property in the app paired with the element that owns it.</summary>
    public IEnumerable<PropertyRef> AllProperties()
    {
        foreach (var p in App.Properties)
            yield return new PropertyRef(OwnerKind.App, App.Name, p);
        foreach (var f in App.Formulas)
            yield return new PropertyRef(OwnerKind.NamedFormula, App.Name, new Property(f.Name, f.Formula, true, f.Location));

        foreach (var screen in Screens)
        {
            foreach (var p in screen.Properties)
                yield return new PropertyRef(OwnerKind.Screen, screen.Name, p);
            foreach (var ctrl in screen.AllControls())
                foreach (var p in ctrl.Properties)
                    yield return new PropertyRef(OwnerKind.Control, ctrl.Name, p, screen.Name);
        }

        foreach (var comp in Components)
        {
            foreach (var p in comp.Properties)
                yield return new PropertyRef(OwnerKind.Component, comp.Name, p);
            foreach (var ctrl in comp.AllControls())
                foreach (var p in ctrl.Properties)
                    yield return new PropertyRef(OwnerKind.Control, ctrl.Name, p, comp.Name);
        }
    }
}

public enum OwnerKind { App, Screen, Control, Component, NamedFormula }

/// <summary>A property together with what owns it, for reporting and analysis.</summary>
public sealed record PropertyRef(
    OwnerKind OwnerKind,
    string OwnerName,
    Property Property,
    string? ScreenName = null)
{
    /// <summary>Human-readable path, e.g. <c>scrHome/galItems.OnSelect</c>.</summary>
    public string Path =>
        ScreenName is { Length: > 0 } && OwnerKind == OwnerKind.Control
            ? $"{ScreenName}/{OwnerName}.{Property.Name}"
            : $"{OwnerName}.{Property.Name}";
}
