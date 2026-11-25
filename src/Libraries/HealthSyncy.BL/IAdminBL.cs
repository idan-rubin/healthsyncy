using HealthSyncy.Core;

namespace HealthSyncy.BL;

public enum SyncStatus { Current, Paused, AwaitingToday, Stale }
public sealed record SyncOverview(SyncSettings Settings, IReadOnlyList<DayRow> Days, SyncStatus Status);

public interface IAdminBL
{
    Task<SyncOverview> Overview(string zone, CancellationToken ct);
    Task SaveSettings(SyncSettings value, CancellationToken ct);
}
