using HealthSyncy.Common.Contracts;
using HealthSyncy.Common.Infrastructure.Repository.Entities;

namespace HealthSyncy.Common.Infrastructure.Repository;

public static class SnapshotQueries
{
    extension(IRepository<SnapshotEntity> snapshots)
    {
        public async Task<DaySnapshot?> GetDayAsync(
            string day,
            string zone,
            CancellationToken cancellationToken = default)
        {
            var stored = await snapshots.GetByIdAsync(SnapshotEntity.IdFor(day, zone), cancellationToken);
            return stored?.ToSnapshot();
        }

        public async Task<IReadOnlyList<DayRow>> GetWeekAsync(
            DateOnly endingOn,
            string zone,
            CancellationToken cancellationToken = default)
        {
            var days = Reporting.WeekEnding(endingOn);
            List<string> ids = [.. days.Select(day => SnapshotEntity.IdFor(day, zone))];
            var found = (await snapshots.QueryAsync(snapshot => ids.Contains(snapshot.Id), cancellationToken))
                .ToDictionary(snapshot => snapshot.Day, snapshot => snapshot.ToSnapshot(), StringComparer.Ordinal);
            return [.. days.Select(day => new DayRow(day, found.GetValueOrDefault(day)))];
        }
    }
}
