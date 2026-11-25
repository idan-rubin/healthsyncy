using System.Linq.Expressions;
using HealthSyncy.Admin.Contracts;
using HealthSyncy.Admin.Domain;
using HealthSyncy.Common.Contracts;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;
using Microsoft.Extensions.Time.Testing;

namespace HealthSyncy.Admin.UnitTests;

public sealed class AdminServiceTests
{
    private const string Zone = "America/New_York";
    private const string Today = "2026-11-01";

    private static readonly DateTimeOffset _now = new(2026, 11, 2, 3, 0, 0, TimeSpan.Zero);

    private readonly AutoMocker _mocker = new();
    private readonly AdminService _service;

    public AdminServiceTests()
    {
        _mocker.Use<TimeProvider>(new FakeTimeProvider(_now));
        Stored();
        _service = _mocker.CreateInstance<AdminService>();
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Mock<IRepository<SettingsEntity>> Settings => _mocker.GetMock<IRepository<SettingsEntity>>();

    [Fact]
    public async Task Lists_the_week_ending_today_in_the_reporting_zone()
    {
        Captured(_now.AddMinutes(-10));

        var overview = await _service.GetOverviewAsync(Zone, Cancellation);

        overview.Days.Select(row => row.Day).Should().Equal(
            "2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29", "2026-10-30", "2026-10-31", Today);
        overview.Days[^1].Snapshot!.CapturedAtUtc.Should().Be(_now.AddMinutes(-10).UtcDateTime);
    }

    [Fact]
    public async Task Is_current_while_todays_snapshot_is_fresh()
    {
        Captured(_now.AddMinutes(-180));

        var overview = await _service.GetOverviewAsync(Zone, Cancellation);

        overview.Status.Should().Be(SyncStatus.Current);
        overview.Settings.Should().Be(SyncSettings.Default);
    }

    [Fact]
    public async Task Is_stale_once_todays_snapshot_is_older_than_the_limit()
    {
        Captured(_now.AddMinutes(-181));

        var overview = await _service.GetOverviewAsync(Zone, Cancellation);

        overview.Status.Should().Be(SyncStatus.Stale);
    }

    [Fact]
    public async Task Honours_the_saved_limit()
    {
        Saved(new(true, 30));
        Captured(_now.AddMinutes(-31));

        var overview = await _service.GetOverviewAsync(Zone, Cancellation);

        overview.Should().BeEquivalentTo(new { Settings = new SyncSettings(true, 30), Status = SyncStatus.Stale });
    }

    [Fact]
    public async Task Awaits_today_until_a_snapshot_arrives()
    {
        var overview = await _service.GetOverviewAsync(Zone, Cancellation);

        overview.Status.Should().Be(SyncStatus.AwaitingToday);
        overview.Days.Should().HaveCount(7).And.OnlyContain(row => row.Snapshot == null);
    }

    [Fact]
    public async Task Is_paused_while_uploads_are_switched_off()
    {
        Saved(new(false, 180));
        Captured(_now.AddMinutes(-10));

        var overview = await _service.GetOverviewAsync(Zone, Cancellation);

        overview.Status.Should().Be(SyncStatus.Paused);
    }

    [Fact]
    public async Task Saves_the_settings()
    {
        await _service.SaveSettingsAsync(new(false, 90), Cancellation);

        Settings.Verify(repository => repository.UpsertAsync(new SettingsEntity("sync", false, 90), Cancellation));
    }

    private void Saved(SyncSettings settings) => Settings
        .Setup(repository => repository.GetByIdAsync(SettingsEntity.SyncId, Cancellation))
        .ReturnsAsync(SettingsEntity.From(settings));

    private void Captured(DateTimeOffset capturedAt) => Stored(new SnapshotEntity(SnapshotEntity.IdFor(Today, Zone),
        Today, Zone, capturedAt.UtcDateTime, capturedAt.UtcDateTime, new(1200, 66, 700, 13, 3, 500, 30, 12)));

    private void Stored(params SnapshotEntity[] snapshots) => _mocker.GetMock<IRepository<SnapshotEntity>>()
        .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<SnapshotEntity, bool>>>(), Cancellation))
        .ReturnsAsync(snapshots);
}
