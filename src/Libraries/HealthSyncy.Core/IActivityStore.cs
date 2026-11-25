namespace HealthSyncy.Core;

public interface IActivityStore
{
    Task Ping(CancellationToken ct);
    Task<DaySnapshot?> SaveIfNewer(DaySnapshot snapshot, CancellationToken ct);
    Task<DaySnapshot?> Get(string day, string zone, CancellationToken ct);
    Task<IReadOnlyList<DaySnapshot>> GetMany(IEnumerable<string> dates, string zone, CancellationToken ct);
    Task<SyncSettings> Settings(CancellationToken ct);
    Task SetSettings(SyncSettings value, CancellationToken ct);
}
