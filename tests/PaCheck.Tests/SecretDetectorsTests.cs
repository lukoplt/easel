using PaCheck.Core.Security;
using Xunit;

namespace PaCheck.Tests;

public sealed class SecretDetectorsTests
{
    [Theory]
    [InlineData("https://user:p4ssw0rd@example.com/api", SecretKind.UrlCredentials)]
    [InlineData("Server=x;Database=y;Password=Sup3rSecretPwd;", SecretKind.ConnectionString)]
    [InlineData("AKIAIOSFODNN7EXAMPLE", SecretKind.ApiKey)]
    [InlineData("xoxb-123456789012-abcdefghijklmnop", SecretKind.Token)]
    public void Detects_known_secret_shapes(string literal, SecretKind expected)
    {
        var kinds = SecretDetectors.Scan(literal).Select(m => m.Kind).ToList();
        Assert.Contains(expected, kinds);
    }

    [Fact]
    public void Flags_high_entropy_string()
    {
        var kinds = SecretDetectors.Scan("f9Kd82hLzQ0pV3mNx7Tb5RwYcA1eUj4").Select(m => m.Kind).ToList();
        Assert.Contains(SecretKind.HighEntropy, kinds);
    }

    [Fact]
    public void Ignores_benign_text()
    {
        Assert.Empty(SecretDetectors.Scan("Please enter your first and last name"));
    }

    [Fact]
    public void Respects_allowlist()
    {
        var opts = SecretScanOptions.Default with { Allowlist = new[] { "EXAMPLE" } };
        Assert.Empty(SecretDetectors.Scan("AKIAIOSFODNN7EXAMPLE", opts));
    }

    [Fact]
    public void Redaction_hides_the_value()
    {
        var match = SecretDetectors.Scan("xoxb-123456789012-abcdefghijklmnop").First();
        Assert.DoesNotContain("123456789012", match.Redacted);
        Assert.Contains("len", match.Redacted);
    }

    [Fact]
    public void Entropy_increases_with_randomness()
    {
        Assert.True(SecretDetectors.ShannonEntropy("aaaaaaaa") < SecretDetectors.ShannonEntropy("a9Fk2Lz8"));
    }
}
