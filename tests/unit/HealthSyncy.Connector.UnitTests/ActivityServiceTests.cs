using System.Linq.Expressions;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;
using HealthSyncy.Connector.Domain;

namespace HealthSyncy.Connector.UnitTests;

public sealed class ActivityServiceTests
{
    private const string Zone = "Asia/Jerusalem";

    private readonly AutoMocker _mocker = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Mock<IRepository<SnapshotEntity>> Snapshots => _mocker.GetMock<IRepository<SnapshotEntity>>();

    [Fact]
    public async Task Reads_one_day_in_the_requested_zone()
    {
        var stored = Entity("2026-11-01");
        Snapshots.Setup(repository => repository.GetByIdAsync("Asia/Jerusalem|2026-11-01", Cancellation))
            .ReturnsAsync(stored);
        var service = _mocker.CreateInstance<ActivityService>();

        var snapshot = await service.GetDayAsync("2026-11-01", Zone, Cancellation);

        snapshot.Should().Be(stored.ToSnapshot());
    }

    [Fact]
    public async Task Reads_the_week_ending_on_a_day_with_gaps_for_missing_days()
    {
        Snapshots
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<SnapshotEntity, bool>>>(), Cancellation))
            .ReturnsAsync([Entity("2026-10-30"), Entity("2026-11-01")]);
        var service = _mocker.CreateInstance<ActivityService>();

        var week = await service.GetWeekAsync(new DateOnly(2026, 11, 1), Zone, Cancellation);

        week.Should().HaveCount(7);
        week.Where(row => row.Snapshot is not null).Select(row => row.Day).Should().Equal("2026-10-30", "2026-11-01");
    }

    private static SnapshotEntity Entity(string day)
    {
        var captured = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);
        return new(SnapshotEntity.IdFor(day, Zone), day, Zone, captured, captured, new(1200, 66, 700, 13, 3, 500, 30, 12));
    }
}
