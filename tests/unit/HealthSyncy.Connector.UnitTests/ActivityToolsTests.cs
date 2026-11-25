using HealthSyncy.Common.Contracts;
using HealthSyncy.Connector.Domain;
using HealthSyncy.Connector.EntryPoint.Tools;
using ModelContextProtocol;

namespace HealthSyncy.Connector.UnitTests;

public sealed class ActivityToolsTests
{
    private static readonly DaySnapshot _snapshot = new("2026-11-01", "UTC",
        new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 18, 1, 0, DateTimeKind.Utc),
        new(1200, 66, 700, 13, 3, 500, 30, 12));

    private readonly AutoMocker _mocker = new();
    private readonly ActivityTools _tools;

    public ActivityToolsTests()
    {
        _tools = _mocker.CreateInstance<ActivityTools>();
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Mock<IActivityService> Activity => _mocker.GetMock<IActivityService>();

    [Fact]
    public async Task Day_tool_returns_the_snapshot_for_the_day()
    {
        Activity.Setup(activity => activity.GetDayAsync("2026-11-01", "UTC", Cancellation)).ReturnsAsync(_snapshot);

        var row = await _tools.GetDay("2026-11-01", "UTC", Cancellation);

        row.Should().Be(new DayRow("2026-11-01", _snapshot));
    }

    [Fact]
    public async Task Day_tool_returns_an_empty_row_for_a_day_without_data()
    {
        var row = await _tools.GetDay("2026-11-01", "UTC", Cancellation);

        row.Should().Be(new DayRow("2026-11-01", null));
    }

    [Fact]
    public async Task Week_tool_returns_the_seven_days_ending_on_the_date()
    {
        IReadOnlyList<DayRow> week = [new("2026-10-31", null), new("2026-11-01", _snapshot)];
        Activity.Setup(activity => activity.GetWeekAsync(new DateOnly(2026, 11, 1), "UTC", Cancellation))
            .ReturnsAsync(week);

        var rows = await _tools.GetWeek("2026-11-01", "UTC", Cancellation);

        rows.Should().BeSameAs(week);
    }

    [Theory]
    [InlineData("2026-02-30", "UTC")]
    [InlineData("yesterday", "UTC")]
    [InlineData("2026-11-01", "Eastern Standard Time")]
    [InlineData("2026-11-01", "")]
    public async Task Tools_reject_malformed_dates_and_zones(string day, string zone)
    {
        var readDay = () => _tools.GetDay(day, zone, Cancellation);
        var readWeek = () => _tools.GetWeek(day, zone, Cancellation);

        (await readDay.Should().ThrowAsync<McpException>()).WithMessage("Use a YYYY-MM-DD date and IANA time zone.");
        (await readWeek.Should().ThrowAsync<McpException>()).WithMessage("Use a YYYY-MM-DD date and IANA time zone.");
        Activity.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Week_tool_rejects_a_date_too_early_to_have_a_full_week()
    {
        var readWeek = () => _tools.GetWeek("0001-01-03", "UTC", Cancellation);

        (await readWeek.Should().ThrowAsync<McpException>()).WithMessage("Date is outside the reporting range.");
    }
}
