using Microsoft.PowerFx.Syntax;

namespace Easel.Fx;

/// <summary>
/// Classifies how a formula uses each bare identifier, so app-level rules can tell a
/// structural reference (control/variable read) from an entity-column mention whose
/// schema easel cannot see:
/// - dotted base (<c>name.Prop</c>) → structural;
/// - bare name outside any record-scope function → structural;
/// - bare name only inside Filter/LookUp/… record scope → column, not structural;
/// - argument of a column-taking function (DataSourceInfo/Validate/Choices) → column.
/// </summary>
public sealed class FxNameClassifier : IdentityTexlVisitor
{
    private static readonly HashSet<string> RecordScopeFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Filter", "LookUp", "Search", "CountIf", "Sum", "Average", "Min", "Max",
        "StdevP", "VarP", "Concat", "ForAll", "AddColumns", "DropColumns",
        "RenameColumns", "ShowColumns", "GroupBy", "Distinct", "Sort", "SortByColumns",
        "RemoveIf", "UpdateIf", "Ungroup", "With",
    };

    private static readonly HashSet<string> ColumnArgFunctions = new(StringComparer.OrdinalIgnoreCase)
        { "DataSourceInfo", "Validate", "Choices" };

    private int _scopeDepth;
    private readonly HashSet<string> _dottedBase = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _bareOutsideScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _bareInScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _columnArgs = new(StringComparer.OrdinalIgnoreCase);

    public static FxNameClassifier Collect(TexlNode root)
    {
        var c = new FxNameClassifier();
        root.Accept(c);
        return c;
    }

    /// <summary>
    /// True when the formula uses <paramref name="name"/> as a control/variable-style
    /// reference rather than (only) as an entity column.
    /// </summary>
    public bool IsStructuralReference(string name)
    {
        if (_columnArgs.Contains(name)) return false;
        return _dottedBase.Contains(name)
               || (_bareOutsideScope.Contains(name) && !_bareInScope.Contains(name));
    }

    public override bool PreVisit(CallNode node)
    {
        var head = node.Head.Name.Value;
        if (RecordScopeFunctions.Contains(head)) _scopeDepth++;
        if (ColumnArgFunctions.Contains(head) && node.Args.ChildNodes is { } args)
            for (var i = 1; i < args.Count; i++)
                if (args[i] is FirstNameNode fn)
                    _columnArgs.Add(fn.Ident.Name.Value);
        return true;
    }

    public override void PostVisit(CallNode node)
    {
        if (RecordScopeFunctions.Contains(node.Head.Name.Value)) _scopeDepth--;
    }

    public override bool PreVisit(DottedNameNode node)
    {
        if (node.Left is FirstNameNode fn) _dottedBase.Add(fn.Ident.Name.Value);
        return true;
    }

    public override void Visit(FirstNameNode node) =>
        (_scopeDepth > 0 ? _bareInScope : _bareOutsideScope).Add(node.Ident.Name.Value);
}
