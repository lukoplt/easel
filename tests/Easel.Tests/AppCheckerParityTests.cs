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

    // --- formula correctness (PF0002–PF0005) ---------------------------------------

    [Fact]
    public void PF0003_skips_With_and_As_scope_names()
    {
        // SampleApp/ComponentApp formulas use With(...) bindings and scope fields —
        // none of them may surface as typos.
        var f = Lint("ComponentApp").Where(x => x.RuleId == "PF0003").ToList();
        var hit = Assert.Single(f);
        Assert.Contains("txtFiltr", hit.Message);
        Assert.Contains("txtFilter", hit.Message);
    }

    [Fact]
    public void PF0004_and_PF0005_do_not_fire_on_valid_formulas()
    {
        var sample = Lint("SampleApp");
        Assert.DoesNotContain(sample, x => x.RuleId == "PF0004");
        Assert.DoesNotContain(sample, x => x.RuleId == "PF0005");
    }

    [Fact]
    public void PF0002_accepts_defined_screens()
    {
        // SampleApp navigates to scrDetail, which exists.
        Assert.DoesNotContain(Lint("SampleApp"), x => x.RuleId == "PF0002");
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("txtFiltr", "txtFilter", 1)]
    [InlineData("same", "same", 0)]
    [InlineData("abc", "xyz", 3)]
    public void Levenshtein_distances(string a, string b, int expected)
    {
        Assert.Equal(expected, PossibleTypoRule.Levenshtein(a, b, 10));
    }

    [Fact]
    public void Levenshtein_early_exits_above_max()
    {
        Assert.True(PossibleTypoRule.Levenshtein("completely", "different", 1) > 1);
    }

    [Theory]
    [InlineData("Operator", "Operators", true)]  // column vs its table — not a typo
    [InlineData("Orders", "Order", true)]
    [InlineData("txtFiltr", "txtFilter", false)] // genuine typo, keep flagging
    public void Plural_pairs_are_exempt_from_typo_detection(string a, string b, bool exempt)
    {
        Assert.Equal(exempt, PossibleTypoRule.IsPluralPair(a, b));
    }

    // --- name classification (shared by PF0003 and PA1019) --------------------------

    [Fact]
    public void Classifier_treats_dotted_base_as_structural()
    {
        var fx = new Easel.Fx.FxParseService();
        var root = fx.Parse("Filter(colItems, StartsWith(Title, txtSerch.Text))").Root!;
        var c = Easel.Fx.FxNameClassifier.Collect(root);
        Assert.True(c.IsStructuralReference("txtSerch"));   // typo-able control read
        Assert.False(c.IsStructuralReference("Title"));     // bare column in scope
    }

    [Fact]
    public void Classifier_treats_column_arguments_as_columns()
    {
        var fx = new Easel.Fx.FxParseService();
        var root = fx.Parse("DataSourceInfo(Tickets, DataSourceInfo.DisplayName, Operator)").Root!;
        var c = Easel.Fx.FxNameClassifier.Collect(root);
        Assert.False(c.IsStructuralReference("Operator"));
        Assert.True(c.IsStructuralReference("Tickets"));
    }

    // --- PA1030 contrast math -------------------------------------------------------

    [Theory]
    [InlineData("RGBA(0, 0, 0, 1)", 0, 0, 0)]
    [InlineData("rgba(255,255,255,1)", 255, 255, 255)]
    public void Rgba_literals_parse(string formula, int r, int g, int b)
    {
        Assert.Equal((r, g, b), LowContrastRule.ParseOpaqueRgba(formula));
    }

    [Theory]
    [InlineData("RGBA(0,0,0,0.5)")] // translucent — inherited background unknowable
    [InlineData("Color.Red")]
    [InlineData("gblTheme.Primary")]
    [InlineData(null)]
    public void Non_opaque_or_computed_colors_are_skipped(string? formula)
    {
        Assert.Null(LowContrastRule.ParseOpaqueRgba(formula));
    }

    [Fact]
    public void Contrast_ratio_black_on_white_is_21()
    {
        Assert.Equal(21.0, LowContrastRule.ContrastRatio((0, 0, 0), (255, 255, 255)), 1);
    }

    [Fact]
    public void Contrast_ratio_grey_on_white_fails_AA()
    {
        Assert.True(LowContrastRule.ContrastRatio((200, 200, 200), (255, 255, 255)) < 4.5);
    }
}
