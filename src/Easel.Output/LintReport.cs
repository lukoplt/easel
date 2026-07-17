using Easel.Core.Model;

namespace Easel.Output;

/// <summary>Severity roll-up for a set of findings.</summary>
public sealed record ReportSummary(int Error, int Warning, int Info)
{
    public int Total => Error + Warning + Info;

    public static ReportSummary From(IEnumerable<Finding> findings)
    {
        int e = 0, w = 0, i = 0;
        foreach (var f in findings)
            switch (f.Severity)
            {
                case Severity.Error: e++; break;
                case Severity.Warning: w++; break;
                default: i++; break;
            }
        return new ReportSummary(e, w, i);
    }
}

/// <summary>Everything a renderer needs to present a lint run.</summary>
public sealed record LintReport(
    string Tool,
    string Version,
    string SchemaVersion,
    string Target,
    IReadOnlyList<Finding> Findings)
{
    public ReportSummary Summary => ReportSummary.From(Findings);
}
