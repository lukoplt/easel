namespace PaCheck.Core.Model;

/// <summary>Severity of a finding. Ordered so comparisons gate on threshold.</summary>
public enum Severity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

/// <summary>Rule category for grouping and reporting.</summary>
public enum RuleCategory
{
    Performance,
    Naming,
    Maintainability,
    Accessibility,
    Security,
    Error,
}
