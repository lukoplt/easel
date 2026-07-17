namespace Easel.Core.Model;

/// <summary>
/// Points at where an element or property came from in the source tree.
/// Line/Column are 1-based; 0 means "unknown" (e.g. synthesised element).
/// </summary>
public readonly record struct SourceLocation(string File, int Line, int Column)
{
    public static readonly SourceLocation Unknown = new(string.Empty, 0, 0);

    public bool IsKnown => !string.IsNullOrEmpty(File);

    public override string ToString() =>
        IsKnown ? (Line > 0 ? $"{File}:{Line}:{Column}" : File) : "<unknown>";
}
