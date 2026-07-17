using System.Runtime.CompilerServices;

namespace Easel.Tests;

/// <summary>Resolves fixture paths from source location — independent of the working directory.</summary>
public static class TestPaths
{
    private static string ThisDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    public static string TestsProjectDir => ThisDir();
    public static string FixturesDir => Path.GetFullPath(Path.Combine(TestsProjectDir, "..", "fixtures"));
    public static string SampleApp => Path.Combine(FixturesDir, "SampleApp");
}
