using System.Runtime.CompilerServices;

namespace Easel.Tests;

internal static class VerifyModuleInit
{
    [ModuleInitializer]
    public static void Init()
    {
        // Headless: never try to launch a diff tool on mismatch.
        Environment.SetEnvironmentVariable("DiffEngine_Disabled", "true");
        VerifierSettings.DisableRequireUniquePrefix();
    }
}
