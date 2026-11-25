namespace HealthSyncy.Core;

public interface ISnapshotStore
{
    Task<DaySnapshot?> SaveIfNewer(DaySnapshot snapshot, CancellationToken ct);
    Task<DaySnapshot?> Get(string day, string zone, CancellationToken ct);
    Task<IReadOnlyList<DaySnapshot>> GetMany(IEnumerable<string> dates, string zone, CancellationToken ct);
}
