namespace Easel.Cli.Infrastructure;

/// <summary>
/// Process-wide cancellation wired to Ctrl+C, so a long-running pac subprocess is stopped
/// promptly instead of blocking for the full timeout.
/// </summary>
public static class AppCancellation
{
    private static readonly CancellationTokenSource Cts = new();

    public static CancellationToken Token => Cts.Token;

    public static void Install()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;   // let us shut down gracefully instead of a hard kill
            Cts.Cancel();
        };
    }
}
