using Microsoft.PowerFx.Syntax;

namespace Easel.Fx;

/// <summary>Structural metrics over a Power Fx AST used by rules (nesting, size).</summary>
public static class AstMetrics
{
    /// <summary>Maximum nesting depth of calls to any of <paramref name="functionNames"/>.</summary>
    public static int MaxCallNesting(TexlNode root, params string[] functionNames)
    {
        var set = new HashSet<string>(functionNames, StringComparer.OrdinalIgnoreCase);
        var v = new NestingVisitor(set);
        root.Accept(v);
        return v.Max;
    }

    /// <summary>True if the subtree contains a call to any of <paramref name="functionNames"/>.</summary>
    public static bool ContainsCall(TexlNode root, params string[] functionNames)
    {
        var set = new HashSet<string>(functionNames, StringComparer.OrdinalIgnoreCase);
        var v = new ContainsCallVisitor(set);
        root.Accept(v);
        return v.Found;
    }

    /// <summary>Total node count — a cheap complexity proxy for stats.</summary>
    public static int NodeCount(TexlNode root)
    {
        var v = new CountingVisitor();
        root.Accept(v);
        return v.Count;
    }

    private sealed class NestingVisitor(HashSet<string> targets) : IdentityTexlVisitor
    {
        private int _depth;
        public int Max { get; private set; }

        public override bool PreVisit(CallNode node)
        {
            if (targets.Contains(node.Head.Name.Value))
            {
                _depth++;
                if (_depth > Max) Max = _depth;
            }
            return true;
        }

        public override void PostVisit(CallNode node)
        {
            if (targets.Contains(node.Head.Name.Value))
                _depth--;
        }
    }

    private sealed class ContainsCallVisitor(HashSet<string> targets) : IdentityTexlVisitor
    {
        public bool Found { get; private set; }
        public override bool PreVisit(CallNode node)
        {
            if (targets.Contains(node.Head.Name.Value)) Found = true;
            return !Found; // stop descending once found
        }
    }

    private sealed class CountingVisitor : IdentityTexlVisitor
    {
        public int Count { get; private set; }
        public override bool PreVisit(CallNode node) { Count++; return true; }
        public override bool PreVisit(BinaryOpNode node) { Count++; return true; }
        public override bool PreVisit(UnaryOpNode node) { Count++; return true; }
        public override bool PreVisit(DottedNameNode node) { Count++; return true; }
        public override bool PreVisit(RecordNode node) { Count++; return true; }
        public override bool PreVisit(TableNode node) { Count++; return true; }
        public override void Visit(FirstNameNode node) => Count++;
        public override void Visit(StrLitNode node) => Count++;
        public override void Visit(NumLitNode node) => Count++;
        public override void Visit(BoolLitNode node) => Count++;
    }
}
