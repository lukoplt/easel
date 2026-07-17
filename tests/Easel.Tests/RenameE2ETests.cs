using Easel.Analysis.Rename;
using Easel.Core;
using Easel.Core.Symbols;
using Easel.Pac;
using Xunit;
using Xunit.Abstractions;

namespace Easel.Tests;

/// <summary>
/// T5.4 — full rename round-trip against a real .msapp: unpack → rename → pack → unpack,
/// then verify consistency. Environment-gated: runs only when EASEL_TEST_MSAPP points at
/// a real .msapp and pac is installed (a proprietary app can't be committed). Skips (as a
/// no-op pass) otherwise, so CI without the app stays green.
/// </summary>
public sealed class RenameE2ETests
{
    private readonly ITestOutputHelper _out;
    public RenameE2ETests(ITestOutputHelper o) => _out = o;

    [Fact]
    public void Rename_round_trips_through_pac()
    {
        var msapp = Environment.GetEnvironmentVariable("EASEL_TEST_MSAPP");
        if (string.IsNullOrWhiteSpace(msapp) || !File.Exists(msapp) || !PacRunner.Detect().Found)
        {
            _out.WriteLine("skipped: set EASEL_TEST_MSAPP to a real .msapp and install pac to run this E2E.");
            return;
        }

        var pac = PacRunner.Create();
        var work = Path.Combine(Path.GetTempPath(), "easel-e2e-" + Guid.NewGuid().ToString("n"));
        var src = Path.Combine(work, "src");
        var outMsapp = Path.Combine(work, "renamed.msapp");
        var reunpack = Path.Combine(work, "reunpack");
        Directory.CreateDirectory(work);

        try
        {
            pac.UnpackMsapp(msapp, src);
            var before = AppAnalysis.FromFolder(src);

            var target = before.Symbols.OfKind(SymbolKind.GlobalVariable).FirstOrDefault()
                         ?? before.Symbols.OfKind(SymbolKind.Collection).FirstOrDefault();
            Assert.NotNull(target); // the app must have at least one variable/collection
            var oldName = target!.Name;
            var newName = oldName + "Renamed";
            _out.WriteLine($"renaming {oldName} -> {newName}");

            var result = RenameEngine.Rename(src, oldName, newName, before);
            Assert.True(result.Success, result.Message);
            Assert.True(result.Occurrences > 0);

            pac.PackMsapp(src, outMsapp);
            Assert.True(File.Exists(outMsapp));

            pac.UnpackMsapp(outMsapp, reunpack);
            var after = AppAnalysis.FromFolder(reunpack);

            // Consistency: structure preserved, rename applied, no stale references.
            Assert.Equal(before.Model.Screens.Count, after.Model.Screens.Count);
            Assert.Equal(before.Model.AllControls().Count(), after.Model.AllControls().Count());
            Assert.True(after.Symbols.IsDefined(newName), "new name should be defined after round-trip");
            Assert.False(after.Symbols.IsDefined(oldName), "old name should be gone after round-trip");
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch { /* best effort */ }
        }
    }
}
