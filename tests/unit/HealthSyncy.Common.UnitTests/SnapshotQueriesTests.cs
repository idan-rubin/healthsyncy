using System.Linq.Expressions;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;

namespace HealthSyncy.Common.UnitTests;

public sealed class SnapshotQueriesTests
{
    private const string Zone = "America/New_York";

    private readonly AutoMocker _mocker = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Mock<IRepository<SnapshotEntity>> Repository => _mocker.GetMock<IRepository<SnapshotEntity>>();

    [Fact]
    public async Task GetDayAsync_reads_the_snapshot_keyed_by_zone_and_day()
    {
        var stored = Entity("2026-11-01");
        Repository.Setup(repository => repository.GetByIdAsync(stored.Id, Cancellation)).ReturnsAsync(stored);

        var snapshot = await Repository.Object.GetDayAsync("2026-11-01", Zone, Cancellation);

        snapshot.Should().Be(stored.ToSnapshot());
    }

    [Fact]
    public async Task GetDayAsync_returns_nothing_for_a_day_without_a_snapshot()
    {
        var snapshot = await Repository.Object.GetDayAsync("2026-11-01", Zone, Cancellation);

        snapshot.Should().BeNull();
    }

    [Fact]
    public async Task GetWeekAsync_lists_seven_days_and_leaves_gaps_empty()
    {
        SnapshotEntity[] stored = [Entity("2026-10-20"), Entity("2026-10-28"), Entity("2026-11-01")];
        Repository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<SnapshotEntity, bool>>>(), Cancellation))
            .ReturnsAsync(IReadOnlyList<SnapshotEntity> (Expression<Func<SnapshotEntity, bool>> predicate,
                CancellationToken _) => [.. stored.Where(predicate.Compile())]);

        var week = await Repository.Object.GetWeekAsync(new DateOnly(2026, 11, 1), Zone, Cancellation);

        week.Select(row => row.Day).Should().Equal(
            "2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29", "2026-10-30", "2026-10-31", "2026-11-01");
        week.Where(row => row.Snapshot is not null).Select(row => row.Day).Should().Equal("2026-10-28", "2026-11-01");
        week[^1].Snapshot.Should().Be(stored[^1].ToSnapshot());
    }

    private static SnapshotEntity Entity(string day)
    {
        var captured = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);
        return new(SnapshotEntity.IdFor(day, Zone), day, Zone, captured, captured, new(1200, 66, 700, 13, 3, 500, 30, 12));
    }
}
