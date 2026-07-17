using System.Security.Cryptography;
using System.Text;

namespace Easel.Core.Model;

/// <summary>
/// One reported issue. Produced by rules and by the secrets/analysis engines.
/// </summary>
public sealed record Finding(
    string RuleId,
    string RuleName,
    RuleCategory Category,
    Severity Severity,
    string Message,
    SourceLocation Location,
    string? ElementPath = null,
    string? Help = null)
{
    /// <summary>
    /// Stable fingerprint for baselining. Deliberately excludes the line number so
    /// unrelated edits above the finding do not invalidate a suppressed baseline entry.
    /// </summary>
    public string Fingerprint()
    {
        var basis = $"{RuleId}{Location.File}{ElementPath}{Message}";
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(basis), hash);
        return Convert.ToHexStringLower(hash)[..16];
    }
}
