using Microsoft.PowerFx.Syntax;

namespace Easel.Fx;

/// <summary>A function call discovered in a formula, with its argument nodes.</summary>
public sealed record FxCall(string Name, string? Namespace, IReadOnlyList<TexlNode> Args, CallNode Node)
{
    public TexlNode? Arg(int i) => i >= 0 && i < Args.Count ? Args[i] : null;
}

/// <summary>An identifier reference (a bare first-name like a variable or data source).</summary>
public sealed record FxName(string Name, int SpanStart);

/// <summary>Kind of state write detected in a formula.</summary>
public enum FxWriteKind { Set, UpdateContext, Collect, ClearCollect }

/// <summary>A state write: the symbol being defined/mutated and where its name token sits.</summary>
public sealed record FxWrite(string Name, FxWriteKind Kind, int TargetSpanStart);

/// <summary>A string literal occurrence.</summary>
public sealed record FxString(string Value, int SpanStart);

/// <summary>Flat facts collected from one formula's AST — the surface rules query.</summary>
public sealed record FxFacts(
    IReadOnlyList<FxCall> Calls,
    IReadOnlyList<FxName> FirstNames,
    IReadOnlyList<FxString> Strings,
    IReadOnlyList<(string Left, string Right)> DottedNames,
    IReadOnlyList<FxWrite> Writes)
{
    public static readonly FxFacts Empty =
        new(Array.Empty<FxCall>(), Array.Empty<FxName>(), Array.Empty<FxString>(),
            Array.Empty<(string, string)>(), Array.Empty<FxWrite>());

    public bool CallsFunction(string name) =>
        Calls.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Collects <see cref="FxFacts"/> from a Power Fx AST. Derives from IdentityTexlVisitor
/// so unhandled node kinds still descend by default.
/// </summary>
public sealed class FxFactsWalker : IdentityTexlVisitor
{
    private readonly List<FxCall> _calls = new();
    private readonly List<FxName> _names = new();
    private readonly List<FxString> _strings = new();
    private readonly List<(string, string)> _dotted = new();
    private readonly List<FxWrite> _writes = new();

    public static FxFacts Collect(TexlNode root)
    {
        var w = new FxFactsWalker();
        root.Accept(w);
        return new FxFacts(w._calls, w._names, w._strings, w._dotted, w._writes);
    }

    public override bool PreVisit(CallNode node)
    {
        var ns = node.Head.Namespace.Length == 0 ? null : node.Head.Namespace.ToString();
        var args = node.Args.ChildNodes?.ToArray() ?? Array.Empty<TexlNode>();
        var name = node.Head.Name.Value;
        _calls.Add(new FxCall(name, ns, args, node));
        CaptureWrite(name, args);
        return true; // keep descending into arguments
    }

    private void CaptureWrite(string fn, IReadOnlyList<TexlNode> args)
    {
        switch (fn)
        {
            case "Set" when args.Count > 0 && args[0] is FirstNameNode s:
                _writes.Add(new FxWrite(s.Ident.Name.Value, FxWriteKind.Set, s.GetCompleteSpan().Min));
                break;
            case "Collect" when args.Count > 0 && args[0] is FirstNameNode c:
                _writes.Add(new FxWrite(c.Ident.Name.Value, FxWriteKind.Collect, c.GetCompleteSpan().Min));
                break;
            case "ClearCollect" when args.Count > 0 && args[0] is FirstNameNode cc:
                _writes.Add(new FxWrite(cc.Ident.Name.Value, FxWriteKind.ClearCollect, cc.GetCompleteSpan().Min));
                break;
            case "UpdateContext" when args.Count > 0 && args[0] is RecordNode rec:
                foreach (var id in rec.Ids)
                    _writes.Add(new FxWrite(id.Name.Value, FxWriteKind.UpdateContext, rec.GetCompleteSpan().Min));
                break;
        }
    }

    public override void Visit(FirstNameNode node) =>
        _names.Add(new FxName(node.Ident.Name.Value, node.GetCompleteSpan().Min));

    public override void Visit(StrLitNode node)
    {
        if (node.Value is not null)
            _strings.Add(new FxString(node.Value, node.GetCompleteSpan().Min));
    }

    public override bool PreVisit(DottedNameNode node)
    {
        var left = (node.Left as FirstNameNode)?.Ident.Name.Value
                   ?? node.Left.GetType().Name;
        _dotted.Add((left, node.Right.Name.Value));
        return true;
    }
}
