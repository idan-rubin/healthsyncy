using System.Globalization;
using HealthSyncy.Common.Contracts;
using HealthSyncy.Mobile.Domain;
using HealthSyncy.Mobile.Domain.Health;
using HealthSyncy.Mobile.Domain.Services;
using Microsoft.Extensions.Time.Testing;

namespace HealthSyncy.Mobile.UnitTests;

public sealed class SummaryServiceTests
{
    private static readonly TimeZoneInfo _newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private readonly AutoMocker _mocker = new();
    private readonly SummaryService _service;

    public SummaryServiceTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var clock = new FakeTimeProvider(new(2026, 11, 2, 3, 30, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(_newYork);
        _mocker.Use<TimeProvider>(clock);
        _service = _mocker.CreateInstance<SummaryService>();
    }

    private Mock<IHealthReader> Reader => _mocker.GetMock<IHealthReader>();

    [Fact]
    public async Task Summarises_what_Health_recorded_today_in_the_phones_zone()
    {
        Reader.Setup(reader => reader.ReadAsync(new DateOnly(2026, 11, 1), _newYork))
            .ReturnsAsync(new DailyTotals(1200, 66.456m, null, 13, 3, 500, 30, 12));

        var summary = await _service.GetTodayAsync();

        summary.Should().Be(string.Join(Environment.NewLine,
            "Apple Health · 2026-11-01 · 22:30 (America/New_York)",
            "Steps: 1200",
            "Active energy: 66.46 kcal",
            "Exercise: 13 min",
            "Stand: 3 h"));
        Reader.Verify(reader => reader.AuthorizeAsync(), Times.Once);
    }

    [Fact]
    public async Task Says_so_when_nothing_is_readable_yet()
    {
        Reader.Setup(reader => reader.ReadAsync(It.IsAny<DateOnly>(), _newYork))
            .ReturnsAsync(new DailyTotals(null, null, null, null, null, 500, 30, 12));

        var summarise = () => _service.GetTodayAsync();

        await summarise.Should().ThrowAsync<SyncException>().WithMessage("No readable activity yet.");
    }
}
