using HealthSyncy.Core;

namespace HealthSyncy.BL;

public interface IActivityBL
{
    Task Ready(CancellationToken ct);
    Task<UploadResult> Accept(string day, Upload upload, CancellationToken ct);
    Task<DaySnapshot?> Day(string day, string zone, CancellationToken ct);
    Task<IReadOnlyList<DayRow>> Week(DateOnly end, string zone, CancellationToken ct);
    Task<SyncOverview> Overview(string zone, CancellationToken ct);
    Task SaveSettings(SyncSettings settings, CancellationToken ct);
}
