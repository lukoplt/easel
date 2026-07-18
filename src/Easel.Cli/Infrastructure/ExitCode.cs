namespace Easel.Cli.Infrastructure;

/// <summary>Process exit codes (see architecture §5.9).</summary>
public static class ExitCode
{
    public const int Ok = 0;
    public const int FindingsOverThreshold = 1;
    public const int InputError = 2;
    public const int PacMissingOrIncompatible = 3;
    public const int InternalError = 4;
    public const int Cancelled = 130;
}

/// <summary>An input the tool cannot process (bad path, pre-YAML, etc.) — maps to exit 2.</summary>
public sealed class InputException(string message) : Exception(message);
