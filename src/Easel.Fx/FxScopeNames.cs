using Microsoft.PowerFx.Syntax;

namespace Easel.Fx;

/// <summary>
/// Collects identifiers a formula introduces itself — <c>With({x: …}, …)</c> record
/// fields and <c>As</c> aliases. These are in scope for the formula even though the
/// app-wide symbol table knows nothing about them, so unknown-name checks must skip
/// them.
/// </summary>
public static class FxScopeNames
{
    public static IReadOnlySet<string> Collect(TexlNode root)
    {
        var v = new Visitor();
        root.Accept(v);
        return v.Names;
    }

    private sealed class Visitor : IdentityTexlVisitor
    {
        public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);

        public override bool PreVisit(CallNode node)
        {
            if (string.Equals(node.Head.Name.Value, "With", StringComparison.OrdinalIgnoreCase)
                && node.Args.ChildNodes is { Count: > 0 } args && args[0] is RecordNode rec)
            {
                foreach (var id in rec.Ids)
                    Names.Add(id.Name.Value);
            }
            return true;
        }

        public override bool PreVisit(AsNode node)
        {
            Names.Add(node.Right.Name.Value);
            return true;
        }
    }
}
