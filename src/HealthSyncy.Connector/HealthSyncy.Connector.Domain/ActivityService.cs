using HealthSyncy.Common.Contracts;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;

namespace HealthSyncy.Connector.Domain;

internal sealed class ActivityService(IRepository<SnapshotEntity> snapshots) : IActivityService
{
    public Task<DaySnapshot?> GetDayAsync(string day, string zone, CancellationToken cancellationToken = default) =>
        snapshots.GetDayAsync(day, zone, cancellationToken);

    public Task<IReadOnlyList<DayRow>> GetWeekAsync(
        DateOnly endingOn,
        string zone,
        CancellationToken cancellationToken = default) =>
        snapshots.GetWeekAsync(endingOn, zone, cancellationToken);
}
