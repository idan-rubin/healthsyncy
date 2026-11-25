using HealthSyncy.Common.Infrastructure.Validation;

namespace HealthSyncy.Common.UnitTests;

public sealed class IanaTimeZoneAttributeTests
{
    private readonly IanaTimeZoneAttribute _attribute = new();

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/London")]
    [InlineData("UTC")]
    [InlineData(null)]
    public void Accepts_IANA_zones_and_leaves_missing_values_to_Required(string? value) =>
        _attribute.IsValid(value).Should().BeTrue();

    [Theory]
    [InlineData("America/NewYork")]
    [InlineData("Eastern Standard Time")]
    [InlineData("")]
    public void Rejects_anything_else(string value) => _attribute.IsValid(value).Should().BeFalse();

    [Fact]
    public void Rejects_values_that_are_not_text() => _attribute.IsValid(42).Should().BeFalse();

    [Fact]
    public void Names_the_setting_in_its_message() =>
        _attribute.FormatErrorMessage("ReportingZone").Should()
            .Be("ReportingZone needs an IANA time zone, such as America/New_York.");
}
