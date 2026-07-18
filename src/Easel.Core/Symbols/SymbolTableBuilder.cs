using Easel.Core.Model;
using Easel.Fx;

namespace Easel.Core.Symbols;

/// <summary>
/// Builds the <see cref="SymbolTable"/> from an <see cref="AppModel"/> and parsed facts.
/// Definitions come from both the structural model (controls/screens/media/sources)
/// and from state-writing calls in formulas (Set/UpdateContext/Collect/ClearCollect).
/// Reads are every bare identifier that is not itself a write target.
/// </summary>
public static class SymbolTableBuilder
{
    public static SymbolTable Build(AppModel model, FxParseService fx)
    {
        var defs = new List<SymbolDefinition>();
        var refs = new List<SymbolReference>();

        // Structural definitions.
        foreach (var s in model.Screens)
            defs.Add(new SymbolDefinition(s.Name, SymbolKind.Screen, null, s.Location, s.Name));
        foreach (var s in model.Screens)
            foreach (var c in s.AllControls())
                defs.Add(new SymbolDefinition(c.Name, SymbolKind.Control, s.Name, c.Location, $"{s.Name}/{c.Name}"));
        foreach (var comp in model.Components)
            foreach (var c in comp.AllControls())
                defs.Add(new SymbolDefinition(c.Name, SymbolKind.Control, comp.Name, c.Location, $"{comp.Name}/{c.Name}"));
        foreach (var d in model.DataSources)
            defs.Add(new SymbolDefinition(d.Name, SymbolKind.DataSource, null, d.Location, d.Name));
        foreach (var m in model.Media)
            defs.Add(new SymbolDefinition(m.Name, SymbolKind.Media, null, m.Location, m.Name));
        foreach (var f in model.App.Formulas)
            defs.Add(new SymbolDefinition(f.Name, SymbolKind.NamedFormula, null, f.Location, $"App.{f.Name}"));

        // Formula-derived definitions and references.
        foreach (var pr in model.AllProperties())
        {
            if (!pr.Property.HasFormula) continue;
            var facts = fx.Facts(pr.Property.Formula);
            var scope = ScopeOf(pr);
            var loc = pr.Property.Location;

            var writeTargets = new HashSet<(string, int)>();
            foreach (var w in facts.Writes)
            {
                writeTargets.Add((w.Name, w.TargetSpanStart));
                var kind = w.Kind switch
                {
                    FxWriteKind.Set => SymbolKind.GlobalVariable,
                    FxWriteKind.UpdateContext => SymbolKind.ContextVariable,
                    FxWriteKind.Collect => SymbolKind.Collection,
                    FxWriteKind.ClearCollect => SymbolKind.Collection,
                    _ => SymbolKind.GlobalVariable,
                };
                var defScope = w.Kind == FxWriteKind.UpdateContext ? scope : null;
                defs.Add(new SymbolDefinition(w.Name, kind, defScope, loc, pr.Path));
            }

            foreach (var n in facts.FirstNames)
            {
                // Skip the identifier token that is a write target (definition, not a read).
                if (writeTargets.Contains((n.Name, n.SpanStart))) continue;
                refs.Add(new SymbolReference(n.Name, scope, loc, pr.Path));
            }
        }

        return new SymbolTable(defs, refs);
    }

    private static string? ScopeOf(PropertyRef pr) => pr.OwnerKind switch
    {
        OwnerKind.Screen => pr.OwnerName,
        OwnerKind.Control => pr.ScreenName,
        OwnerKind.Component => pr.OwnerName,
        _ => null,
    };
}
