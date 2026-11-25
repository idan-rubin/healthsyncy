using HealthSyncy.Core;

namespace HealthSyncy.BL;

public sealed class AdminBL(IActivityBL activity, ISettingsStore settings) : IAdminBL
{
    public async Task<SyncOverview> Overview(string zone, CancellationToken ct)
    {
        var current = await settings.Get(ct);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, zone).DateTime);
        var days = await activity.Week(today, zone, ct);
        var latest = days[^1].Snapshot;
        var status = !current.Enabled ? SyncStatus.Paused :
            latest is null ? SyncStatus.AwaitingToday :
            now.UtcDateTime - latest.CapturedAtUtc > TimeSpan.FromMinutes(current.StaleAfterMinutes) ? SyncStatus.Stale :
            SyncStatus.Current;
        return new(current, days, status);
    }

    public Task SaveSettings(SyncSettings value, CancellationToken ct) => settings.Set(value, ct);
}
