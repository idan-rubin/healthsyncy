using HealthSyncy.Common.Contracts;

namespace HealthSyncy.Common.UnitTests;

public sealed class ReportingTests
{
    [Theory]
    [InlineData("2026-11-01", true)]
    [InlineData("2028-02-29", true)]
    [InlineData("2026-02-30", false)]
    [InlineData("2026-1-1", false)]
    [InlineData("01/11/2026", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDay_accepts_only_ISO_calendar_days(string? day, bool expected) =>
        Reporting.IsDay(day).Should().Be(expected);

    [Theory]
    [InlineData("America/New_York", true)]
    [InlineData("Asia/Jerusalem", true)]
    [InlineData("UTC", true)]
    [InlineData("Eastern Standard Time", false)]
    [InlineData("invalid/timezone", false)]
    [InlineData(" ", false)]
    [InlineData(null, false)]
    public void IsZone_accepts_only_IANA_zones(string? zone, bool expected) =>
        Reporting.IsZone(zone).Should().Be(expected);

    [Fact]
    public void IsZone_rejects_oversized_input() => Reporting.IsZone(new string('a', 101)).Should().BeFalse();

    [Theory]
    [InlineData("America/New_York", "2026-11-01")]
    [InlineData("UTC", "2026-11-02")]
    [InlineData("Asia/Tokyo", "2026-11-02")]
    public void DayIn_gives_the_local_calendar_day(string zone, string expected) =>
        Reporting.DayIn(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero), zone).Should().Be(expected);

    [Fact]
    public void WeekEnding_lists_seven_days_oldest_first() =>
        Reporting.WeekEnding(new DateOnly(2026, 11, 1)).Should().Equal(
            "2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29", "2026-10-30", "2026-10-31", "2026-11-01");
}
