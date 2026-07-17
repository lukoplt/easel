using System.Diagnostics;
using System.Text;

namespace Easel.Pac;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
    public string Combined => (StdOut + "\n" + StdErr).Trim();
}

/// <summary>Runs an external process and captures its output. No shell interpolation.</summary>
public static class ProcessRunner
{
    public static ProcessResult Run(string fileName, IEnumerable<string> args, int timeoutMs = 120_000,
        Action<string>? onStdErrLine = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderr.AppendLine(e.Data);
            onStdErrLine?.Invoke(e.Data);
        };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return new ProcessResult(-1, stdout.ToString(), $"process timed out after {timeoutMs} ms");
        }
        p.WaitForExit(); // flush async buffers
        return new ProcessResult(p.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
