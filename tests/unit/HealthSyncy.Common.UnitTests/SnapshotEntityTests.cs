using HealthSyncy.Common.Contracts;
using HealthSyncy.Common.Infrastructure.Repository.Entities;

namespace HealthSyncy.Common.UnitTests;

public sealed class SnapshotEntityTests
{
    [Fact]
    public void Lives_in_the_days_collection() => SnapshotEntity.Collection.Should().Be("days");

    [Fact]
    public void Is_keyed_by_zone_and_day() =>
        SnapshotEntity.IdFor("2026-11-01", "America/New_York").Should().Be("America/New_York|2026-11-01");

    [Fact]
    public void Maps_to_the_snapshot_contract()
    {
        var captured = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc);
        var totals = new DailyTotals(1200, 66, 700, 13, 3, 500, 30, 12);
        var entity = new SnapshotEntity("UTC|2026-11-01", "2026-11-01", "UTC", captured, captured.AddMinutes(1), totals);

        entity.ToSnapshot().Should().Be(new DaySnapshot("2026-11-01", "UTC", captured, captured.AddMinutes(1), totals));
    }
}
