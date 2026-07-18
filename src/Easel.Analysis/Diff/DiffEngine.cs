using Easel.Core.Model;
using Easel.Fx;

namespace Easel.Analysis.Diff;

public enum ChangeKind { Added, Removed, Renamed, PropertyChanged, Moved }

public sealed record ElementChange(
    ChangeKind Kind,
    string ElementKind,
    string Name,
    string? Detail = null);

public sealed record DiffReport(IReadOnlyList<ElementChange> Changes)
{
    public int Count(ChangeKind k) => Changes.Count(c => c.Kind == k);
    public bool IsEmpty => Changes.Count == 0;
}

/// <summary>Semantic diff of two app models: matches elements, classifies changes.</summary>
public static class DiffEngine
{
    private sealed record FlatControl(string Name, string Type, string Screen, string Parent, Control Control)
    {
        public string ScopedKey => $"{Screen}\0{Name}";
    }

    public static DiffReport Diff(AppModel baseModel, AppModel headModel, double renameThreshold = 0.6)
    {
        var fx = new FxParseService();
        var changes = new List<ElementChange>();

        // --- Screens (added / removed / property changes) ---
        var baseScreens = baseModel.Screens.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var headScreens = headModel.Screens.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var name in headScreens.Keys.Where(n => !baseScreens.ContainsKey(n)))
            changes.Add(new ElementChange(ChangeKind.Added, "Screen", name));
        foreach (var name in baseScreens.Keys.Where(n => !headScreens.ContainsKey(n)))
            changes.Add(new ElementChange(ChangeKind.Removed, "Screen", name));
        foreach (var (name, headS) in headScreens)
            if (baseScreens.TryGetValue(name, out var baseS))
                foreach (var change in PropertyChanges(baseS.Properties, headS.Properties, fx))
                    changes.Add(change with { Name = $"{name}.{change.Name}" });

        // --- Controls ---
        var baseFlat = Flatten(baseModel).ToList();
        var headFlat = Flatten(headModel).ToList();
        var duplicatedNames = baseFlat
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.GroupBy(c => c.Screen, StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => g.Key)
            .Concat(headFlat
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.GroupBy(c => c.Screen, StringComparer.OrdinalIgnoreCase).Count() > 1)
                .Select(g => g.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string Key(FlatControl c) => duplicatedNames.Contains(c.Name) ? c.ScopedKey : c.Name;
        var baseControls = baseFlat.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
        var headControls = headFlat.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);

        var added = headControls.Where(pair => !baseControls.ContainsKey(pair.Key)).ToList();
        var removed = baseControls.Where(pair => !headControls.ContainsKey(pair.Key)).ToList();

        // Rename heuristic: pair added<->removed of same type by property similarity.
        var renamedFrom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var renamedTo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in added)
        {
            var best = removed
                .Where(r => !renamedFrom.Contains(r.Key) && r.Value.Type == a.Value.Type)
                .Select(r => (r, sim: Similarity(r.Value.Control, a.Value.Control)))
                .OrderByDescending(x => x.sim)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(best.r.Key) && best.sim >= renameThreshold)
            {
                renamedFrom.Add(best.r.Key);
                renamedTo.Add(a.Key);
                changes.Add(new ElementChange(ChangeKind.Renamed, best.r.Value.Type,
                    $"{best.r.Value.Name} → {a.Value.Name}", $"probable rename ({best.sim:P0} property match)"));
            }
        }

        foreach (var c in added.Where(c => !renamedTo.Contains(c.Key)).Select(c => c.Value))
            changes.Add(new ElementChange(ChangeKind.Added, c.Type, c.Name, $"on {c.Screen}"));
        foreach (var c in removed.Where(c => !renamedFrom.Contains(c.Key)).Select(c => c.Value))
            changes.Add(new ElementChange(ChangeKind.Removed, c.Type, c.Name, $"from {c.Screen}"));

        // --- Matched controls: moved + property changes ---
        foreach (var (key, headC) in headControls)
        {
            if (!baseControls.TryGetValue(key, out var baseC)) continue;

            if (!string.Equals(baseC.Parent, headC.Parent, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(baseC.Screen, headC.Screen, StringComparison.OrdinalIgnoreCase))
                changes.Add(new ElementChange(ChangeKind.Moved, headC.Type, headC.Name,
                    $"{baseC.Screen}/{baseC.Parent} → {headC.Screen}/{headC.Parent}"));

            foreach (var change in PropertyChanges(baseC.Control.Properties, headC.Control.Properties, fx))
                changes.Add(change with { Name = $"{headC.Name}.{change.Name}" });
        }

        // --- App properties ---
        foreach (var change in PropertyChanges(baseModel.App.Properties, headModel.App.Properties, fx))
            changes.Add(change with { Name = $"App.{change.Name}" });

        // --- Named formulas ---
        DiffByName(changes, "NamedFormula",
            baseModel.App.Formulas.ToDictionary(f => f.Name, f => f.Formula, StringComparer.OrdinalIgnoreCase),
            headModel.App.Formulas.ToDictionary(f => f.Name, f => f.Formula, StringComparer.OrdinalIgnoreCase));

        // --- Components (definitions + their properties) ---
        var baseComp = baseModel.Components.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var headComp = headModel.Components.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var n in headComp.Keys.Where(k => !baseComp.ContainsKey(k))) changes.Add(new ElementChange(ChangeKind.Added, "Component", n));
        foreach (var n in baseComp.Keys.Where(k => !headComp.ContainsKey(k))) changes.Add(new ElementChange(ChangeKind.Removed, "Component", n));
        foreach (var (n, hc) in headComp)
            if (baseComp.TryGetValue(n, out var bc))
                foreach (var change in PropertyChanges(bc.Properties, hc.Properties, fx))
                    changes.Add(change with { Name = $"{n}.{change.Name}" });

        // --- Data sources (name + metadata) ---
        DiffMetadata(changes, "DataSource",
            baseModel.DataSources.ToDictionary(d => d.Name, d => $"{d.Type}|{d.ConnectorId}", StringComparer.OrdinalIgnoreCase),
            headModel.DataSources.ToDictionary(d => d.Name, d => $"{d.Type}|{d.ConnectorId}", StringComparer.OrdinalIgnoreCase));

        // --- Media (name + metadata) ---
        DiffMetadata(changes, "Media",
            baseModel.Media.ToDictionary(m => m.Name, m => $"{m.FileName}|{m.Kind}|{m.SizeBytes}", StringComparer.OrdinalIgnoreCase),
            headModel.Media.ToDictionary(m => m.Name, m => $"{m.FileName}|{m.Kind}|{m.SizeBytes}", StringComparer.OrdinalIgnoreCase));

        return new DiffReport(changes);
    }

    /// <summary>Added / removed by name, plus a PropertyChanged when the metadata string differs.</summary>
    private static void DiffMetadata(List<ElementChange> changes, string kind,
        IReadOnlyDictionary<string, string> baseMap, IReadOnlyDictionary<string, string> headMap)
    {
        foreach (var (name, hv) in headMap)
        {
            if (!baseMap.TryGetValue(name, out var bv)) changes.Add(new ElementChange(ChangeKind.Added, kind, name));
            else if (bv != hv) changes.Add(new ElementChange(ChangeKind.PropertyChanged, kind, name, "metadata changed"));
        }
        foreach (var name in baseMap.Keys.Where(k => !headMap.ContainsKey(k)))
            changes.Add(new ElementChange(ChangeKind.Removed, kind, name));
    }

    private static void DiffByName(List<ElementChange> changes, string kind,
        IReadOnlyDictionary<string, string> baseMap, IReadOnlyDictionary<string, string> headMap)
    {
        foreach (var (name, hv) in headMap)
        {
            if (!baseMap.TryGetValue(name, out var bv))
                changes.Add(new ElementChange(ChangeKind.Added, kind, name));
            else if (Normalize(bv) != Normalize(hv))
                changes.Add(new ElementChange(ChangeKind.PropertyChanged, kind, name, "changed"));
        }
        foreach (var name in baseMap.Keys.Where(k => !headMap.ContainsKey(k)))
            changes.Add(new ElementChange(ChangeKind.Removed, kind, name));
    }

    private static IEnumerable<ElementChange> PropertyChanges(
        IReadOnlyList<Property> baseList, IReadOnlyList<Property> headList, FxParseService fx)
    {
        var baseProps = baseList.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var headProps = headList.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (pname, hp) in headProps)
        {
            if (!baseProps.TryGetValue(pname, out var bp))
            {
                yield return new ElementChange(ChangeKind.Added, "Property", pname, "property added");
                continue;
            }
            if (Normalize(bp.Formula) == Normalize(hp.Formula)) continue;
            yield return new ElementChange(ChangeKind.PropertyChanged, "Property", pname, SemanticDelta(bp.Formula, hp.Formula, fx));
        }
        foreach (var pname in baseProps.Keys.Where(k => !headProps.ContainsKey(k)))
            yield return new ElementChange(ChangeKind.Removed, "Property", pname, "property removed");
    }

    /// <summary>Describe what changed at AST level (functions/identifiers added/removed), not text.</summary>
    private static string SemanticDelta(string before, string after, FxParseService fx)
    {
        var fb = fx.Facts(before);
        var fa = fx.Facts(after);
        var callsBefore = fb.Calls.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var callsAfter = fa.Calls.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var namesBefore = fb.FirstNames.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var namesAfter = fa.FirstNames.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parts = new List<string>();
        AddDelta(parts, "fn", callsAfter.Except(callsBefore), callsBefore.Except(callsAfter));
        AddDelta(parts, "ref", namesAfter.Except(namesBefore), namesBefore.Except(namesAfter));
        return parts.Count > 0 ? string.Join("; ", parts) : "formula edited";
    }

    private static void AddDelta(List<string> parts, string label, IEnumerable<string> added, IEnumerable<string> removed)
    {
        var a = added.ToList();
        var r = removed.ToList();
        if (a.Count > 0) parts.Add($"+{label} {string.Join(",", a)}");
        if (r.Count > 0) parts.Add($"-{label} {string.Join(",", r)}");
    }

    private static double Similarity(Control a, Control b)
    {
        var pa = a.Properties.ToDictionary(p => p.Name, p => Normalize(p.Formula), StringComparer.OrdinalIgnoreCase);
        var pb = b.Properties.ToDictionary(p => p.Name, p => Normalize(p.Formula), StringComparer.OrdinalIgnoreCase);
        var keys = pa.Keys.Union(pb.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        if (keys.Count == 0) return 1.0;
        int same = keys.Count(k => pa.TryGetValue(k, out var va) && pb.TryGetValue(k, out var vb) && va == vb);
        return (double)same / keys.Count;
    }

    private static IEnumerable<FlatControl> Flatten(AppModel model)
    {
        foreach (var s in model.Screens)
            foreach (var c in Walk(s.Children, s.Name, s.Name))
                yield return c;
        foreach (var comp in model.Components)
            foreach (var c in Walk(comp.Children, comp.Name, comp.Name))
                yield return c;
    }

    private static IEnumerable<FlatControl> Walk(IReadOnlyList<Control> controls, string screen, string parent)
    {
        foreach (var c in controls)
        {
            yield return new FlatControl(c.Name, BaseType(c), screen, parent, c);
            foreach (var d in Walk(c.Children, screen, c.Name))
                yield return d;
        }
    }

    private static string BaseType(Control c)
    {
        var slash = c.ControlType.LastIndexOf('/');
        return slash >= 0 ? c.ControlType[(slash + 1)..] : c.ControlType;
    }

    /// <summary>
    /// Strip whitespace that is not significant — but keep whitespace INSIDE string literals,
    /// so ="a b" and ="ab" are treated as different. Power Fx escapes a quote by doubling it.
    /// </summary>
    private static string Normalize(string f)
    {
        var sb = new System.Text.StringBuilder(f.Length);
        char quote = '\0';   // '\0' = outside; '"' = string; '\'' = quoted identifier
        foreach (var c in f)
        {
            if (quote == '\0' && (c == '"' || c == '\'')) { quote = c; sb.Append(c); }
            else if (quote != '\0' && c == quote) { quote = '\0'; sb.Append(c); }
            else if (quote != '\0' || !char.IsWhiteSpace(c)) sb.Append(c);
        }
        return sb.ToString();
    }
}
