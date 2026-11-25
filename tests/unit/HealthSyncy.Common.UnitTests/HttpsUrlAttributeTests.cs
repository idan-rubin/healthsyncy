using HealthSyncy.Common.Infrastructure.Validation;

namespace HealthSyncy.Common.UnitTests;

public sealed class HttpsUrlAttributeTests
{
    private readonly HttpsUrlAttribute _attribute = new();

    [Theory]
    [InlineData("https://example.org")]
    [InlineData("https://example.org/mcp")]
    [InlineData("https://example.org:8443/realms/health")]
    [InlineData(null)]
    public void Accepts_HTTPS_URLs_and_leaves_missing_values_to_Required(string? value) =>
        _attribute.IsValid(value).Should().BeTrue();

    [Theory]
    [InlineData("http://example.org")]
    [InlineData("https://user:secret@example.org")]
    [InlineData("https://example.org/?debug=1")]
    [InlineData("https://example.org/#fragment")]
    [InlineData("example.org")]
    [InlineData("")]
    public void Rejects_anything_else(string value) => _attribute.IsValid(value).Should().BeFalse();

    [Fact]
    public void Rejects_values_that_are_not_text() => _attribute.IsValid(42).Should().BeFalse();

    [Fact]
    public void Names_the_setting_in_its_message() =>
        _attribute.FormatErrorMessage("OAuthIssuer").Should().Be("OAuthIssuer needs an HTTPS URL.");
}
