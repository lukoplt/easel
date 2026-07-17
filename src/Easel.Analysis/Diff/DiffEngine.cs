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
    private sealed record FlatControl(string Name, string Type, string Screen, string Parent, Control Control);

    public static DiffReport Diff(AppModel baseModel, AppModel headModel, double renameThreshold = 0.6)
    {
        var fx = new FxParseService();
        var changes = new List<ElementChange>();

        // --- Screens ---
        var baseScreens = baseModel.Screens.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        var headScreens = headModel.Screens.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var name in headScreens.Keys.Where(n => !baseScreens.ContainsKey(n)))
            changes.Add(new ElementChange(ChangeKind.Added, "Screen", name));
        foreach (var name in baseScreens.Keys.Where(n => !headScreens.ContainsKey(n)))
            changes.Add(new ElementChange(ChangeKind.Removed, "Screen", name));

        // --- Controls ---
        var baseControls = Flatten(baseModel).ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var headControls = Flatten(headModel).ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var added = headControls.Values.Where(c => !baseControls.ContainsKey(c.Name)).ToList();
        var removed = baseControls.Values.Where(c => !headControls.ContainsKey(c.Name)).ToList();

        // Rename heuristic: pair added<->removed of same type by property similarity.
        var renamedFrom = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var renamedTo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in added)
        {
            var best = removed
                .Where(r => !renamedFrom.Contains(r.Name) && r.Type == a.Type)
                .Select(r => (r, sim: Similarity(r.Control, a.Control)))
                .OrderByDescending(x => x.sim)
                .FirstOrDefault();
            if (best.r is not null && best.sim >= renameThreshold)
            {
                renamedFrom.Add(best.r.Name);
                renamedTo.Add(a.Name);
                changes.Add(new ElementChange(ChangeKind.Renamed, best.r.Type,
                    $"{best.r.Name} → {a.Name}", $"probable rename ({best.sim:P0} property match)"));
            }
        }

        foreach (var c in added.Where(c => !renamedTo.Contains(c.Name)))
            changes.Add(new ElementChange(ChangeKind.Added, c.Type, c.Name, $"on {c.Screen}"));
        foreach (var c in removed.Where(c => !renamedFrom.Contains(c.Name)))
            changes.Add(new ElementChange(ChangeKind.Removed, c.Type, c.Name, $"from {c.Screen}"));

        // --- Matched controls: moved + property changes ---
        foreach (var (name, headC) in headControls)
        {
            if (!baseControls.TryGetValue(name, out var baseC)) continue;

            if (!string.Equals(baseC.Parent, headC.Parent, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(baseC.Screen, headC.Screen, StringComparison.OrdinalIgnoreCase))
                changes.Add(new ElementChange(ChangeKind.Moved, headC.Type, name,
                    $"{baseC.Screen}/{baseC.Parent} → {headC.Screen}/{headC.Parent}"));

            foreach (var change in PropertyChanges(baseC.Control, headC.Control, fx))
                changes.Add(change with { Name = $"{name}.{change.Name}" });
        }

        return new DiffReport(changes);
    }

    private static IEnumerable<ElementChange> PropertyChanges(Control baseC, Control headC, FxParseService fx)
    {
        var baseProps = baseC.Properties.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var headProps = headC.Properties.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var (pname, hp) in headProps)
        {
            if (!baseProps.TryGetValue(pname, out var bp)) continue; // new property: implied by other tooling
            if (Normalize(bp.Formula) == Normalize(hp.Formula)) continue;

            var detail = SemanticDelta(bp.Formula, hp.Formula, fx);
            yield return new ElementChange(ChangeKind.PropertyChanged, "Property", pname, detail);
        }
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

    private static string Normalize(string f) => string.Concat(f.Where(ch => !char.IsWhiteSpace(ch)));
}
