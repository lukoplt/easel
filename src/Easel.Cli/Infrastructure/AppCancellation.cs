namespace Easel.Cli.Infrastructure;

/// <summary>
/// Process-wide cancellation wired to Ctrl+C, so a long-running pac subprocess is stopped
/// promptly instead of blocking for the full timeout.
/// </summary>
public static class AppCancellation
{
    private static readonly CancellationTokenSource Cts = new();
    private static int _activeOperations;

    public static CancellationToken Token => Cts.Token;

    public static void Install()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            // Only swallow Ctrl+C while code is actively observing this token. Other commands
            // retain the operating system's normal immediate-termination behaviour.
            if (Volatile.Read(ref _activeOperations) <= 0) return;
            e.Cancel = true;
            Cts.Cancel();
        };
    }

    public static IDisposable BeginOperation()
    {
        Interlocked.Increment(ref _activeOperations);
        return new OperationScope();
    }

    private sealed class OperationScope : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Interlocked.Decrement(ref _activeOperations);
        }
    }
}
