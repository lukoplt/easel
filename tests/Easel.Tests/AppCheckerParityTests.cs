using Easel.Core;
using Easel.Core.Config;
using Easel.Core.Model;
using Easel.Rules;
using Easel.Rules.Builtin;
using Xunit;

namespace Easel.Tests;

/// <summary>
/// Targeted tests for the App-checker-parity rules: token-precise media matching
/// (PA1004), cross-screen references (PA1019), and the negative cases the fixture
/// coverage test cannot express.
/// </summary>
public sealed class AppCheckerParityTests
{
    // --- PA1004 whole-token media matching -----------------------------------------

    [Theory]
    [InlineData("https://cdn.contoso.com/logo.png", "logo", true)]   // path segment
    [InlineData("logo", "logo", true)]                               // exact
    [InlineData("show logo now", "logo", true)]                      // word boundary
    [InlineData("logotype", "logo", false)]                          // substring only
    [InlineData("catalogo", "logo", false)]                          // substring only
    [InlineData("iconography", "icon", false)]                       // substring only
    [InlineData("LOGO.PNG", "logo.png", true)]                       // case-insensitive
    [InlineData("", "logo", false)]
    public void Media_token_matching(string literal, string token, bool expected)
    {
        Assert.Equal(expected, UnusedMediaRule.ContainsToken(literal, token));
    }

    // --- fixture-driven negatives ---------------------------------------------------

    private static IReadOnlyList<Finding> Lint(string fixture)
    {
        var analysis = AppAnalysis.FromFolder(Path.Combine(TestPaths.FixturesDir, fixture));
        return RuleEngine.CreateDefault().Run(analysis, EaselConfig.Empty);
    }

    [Fact]
    public void PA1019_does_not_flag_same_screen_references()
    {
        // SampleApp: galItems reads txtSearch.Text on the same screen — not a finding.
        var f = Lint("SampleApp").Where(x => x.RuleId == "PA1019").ToList();
        Assert.DoesNotContain(f, x => x.Message.Contains("txtSearch"));
    }

    [Fact]
    public void PA1027_stays_silent_without_StartScreen()
    {
        // SampleApp has no App.StartScreen — the start screen is unknowable from
        // source, so unused-screen must not guess.
        Assert.DoesNotContain(Lint("SampleApp"), x => x.RuleId == "PA1027");
    }

    [Fact]
    public void PA1027_exempts_the_start_screen()
    {
        var f = Lint("ComponentApp").Where(x => x.RuleId == "PA1027").ToList();
        Assert.DoesNotContain(f, x => x.Message.Contains("scrOrders"));
        Assert.Contains(f, x => x.Message.Contains("scrArchive"));
        Assert.Contains(f, x => x.Message.Contains("Screen1"));
    }

    [Fact]
    public void PA1028_flags_the_text_input_not_the_query_formula()
    {
        var f = Lint("ComponentApp").Where(x => x.RuleId == "PA1028").ToList();
        var hit = Assert.Single(f);
        Assert.Contains("txtFilter", hit.Message);
        Assert.Equal("scrOrders/txtFilter", hit.ElementPath);
    }

    [Fact]
    public void PA1019_reports_the_referencing_formula()
    {
        var f = Lint("ComponentApp").Where(x => x.RuleId == "PA1019").ToList();
        Assert.Contains(f, x => x.Message.Contains("btnSync") && x.Message.Contains("scrArchive"));
    }

    [Fact]
    public void CleanApp_still_has_no_findings_with_new_rules()
    {
        Assert.Empty(Lint("CleanApp"));
    }
}
