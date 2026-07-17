using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PaCheck.Pac;

public sealed record PacInfo(bool Found, string? Path, string? Version, bool VersionSupported);

/// <summary>Raised for pac-related failures the CLI maps to exit code 3.</summary>
public sealed class PacException(string message) : Exception(message);

/// <summary>
/// Locates and drives the Power Platform CLI (<c>pac</c>) for unpack/pack. pac is the only
/// supported way to open .msapp/solution inputs — pacheck never re-implements it.
/// </summary>
public sealed partial class PacRunner
{
    /// <summary>Minimum pac version pacheck is tested against.</summary>
    public static readonly Version MinSupportedVersion = new(1, 30, 0);

    public const string InstallInstruction =
        "pac CLI not found. Install it with:\n" +
        "  dotnet tool install --global Microsoft.PowerApps.CLI.Tool\n" +
        "or via the Power Platform Tools VS Code extension.";

    private readonly string _pacPath;

    private PacRunner(string pacPath) => _pacPath = pacPath;

    [GeneratedRegex(@"Version:\s*([0-9]+\.[0-9]+\.[0-9]+)")]
    private static partial Regex VersionRegex();

    /// <summary>Detect pac and its version without throwing.</summary>
    public static PacInfo Detect()
    {
        var path = FindExecutable();
        if (path is null) return new PacInfo(false, null, null, false);

        string? version = null;
        try
        {
            var res = ProcessRunner.Run(path, new[] { "help" }, timeoutMs: 30_000);
            var m = VersionRegex().Match(res.Combined);
            if (m.Success) version = m.Groups[1].Value;
        }
        catch { /* detection is best-effort */ }

        var supported = version is not null
            && Version.TryParse(version, out var v)
            && v >= MinSupportedVersion;
        return new PacInfo(true, path, version, supported);
    }

    /// <summary>Create a runner or throw a <see cref="PacException"/> with install guidance.</summary>
    public static PacRunner Create()
    {
        var info = Detect();
        if (!info.Found || info.Path is null)
            throw new PacException(InstallInstruction);
        if (!info.VersionSupported)
            throw new PacException(
                $"pac {info.Version ?? "?"} is older than the minimum supported {MinSupportedVersion}. Please update pac.");
        return new PacRunner(info.Path);
    }

    /// <summary>Unpack a .msapp into <paramref name="destination"/> via <c>pac canvas unpack</c>.</summary>
    public void UnpackMsapp(string msappPath, string destination, Action<string>? onPacLine = null)
    {
        Directory.CreateDirectory(destination);
        var res = ProcessRunner.Run(_pacPath,
            new[] { "canvas", "unpack", "--msapp", msappPath, "--sources", destination },
            timeoutMs: 300_000,
            onStdErrLine: line => onPacLine?.Invoke($"[pac] {line}"));

        if (!res.Success)
            throw new PacException($"pac canvas unpack failed (exit {res.ExitCode}).\n{res.Combined}");
    }

    /// <summary>Pack an unpacked source folder into a new .msapp via <c>pac canvas pack</c>.</summary>
    public void PackMsapp(string sourceFolder, string msappOut, Action<string>? onPacLine = null)
    {
        var res = ProcessRunner.Run(_pacPath,
            new[] { "canvas", "pack", "--msapp", msappOut, "--sources", sourceFolder },
            timeoutMs: 300_000,
            onStdErrLine: line => onPacLine?.Invoke($"[pac] {line}"));

        if (!res.Success)
            throw new PacException($"pac canvas pack failed (exit {res.ExitCode}).\n{res.Combined}");
    }

    private static string? FindExecutable()
    {
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pac.exe" : "pac";

        // 1. PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), exeName);
            if (File.Exists(candidate)) return candidate;
        }

        // 2. Typical install locations.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var candidate in CandidatePaths(home, exeName))
            if (File.Exists(candidate)) return candidate;

        return null;
    }

    private static IEnumerable<string> CandidatePaths(string home, string exeName)
    {
        yield return Path.Combine(home, ".dotnet", "tools", exeName);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            yield return Path.Combine(localApp, "Microsoft", "PowerAppsCLI", exeName);
        }
        else
        {
            yield return Path.Combine("/usr/local/bin", exeName);
            yield return Path.Combine("/opt/homebrew/bin", exeName);
        }
    }

    /// <summary>Deterministic temp folder for an input path (content of the source path + size).</summary>
    public static string TempFolderFor(string inputPath)
    {
        var basis = Path.GetFullPath(inputPath);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(basis)))[..12];
        return Path.Combine(Path.GetTempPath(), "pacheck", hash);
    }
}
