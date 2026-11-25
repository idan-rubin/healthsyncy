using HealthSyncy.Admin.Contracts;
using HealthSyncy.Common.Contracts;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;

namespace HealthSyncy.Admin.Domain;

internal sealed class AdminService(
    IRepository<SnapshotEntity> snapshots,
    IRepository<SettingsEntity> settings,
    TimeProvider time) : IAdminService
{
    public async Task<SyncOverview> GetOverviewAsync(string zone, CancellationToken cancellationToken = default)
    {
        var current = await settings.GetSyncSettingsAsync(cancellationToken);
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, zone).DateTime);
        var days = await snapshots.GetWeekAsync(today, zone, cancellationToken);
        var status = (current, days[^1].Snapshot) switch
        {
            ({ Enabled: false }, _) => SyncStatus.Paused,
            (_, null) => SyncStatus.AwaitingToday,
            ({ StaleAfterMinutes: var limit }, { CapturedAtUtc: var captured })
                when now.UtcDateTime - captured > TimeSpan.FromMinutes(limit) => SyncStatus.Stale,
            _ => SyncStatus.Current
        };
        return new(current, days, status);
    }

    public Task SaveSettingsAsync(SyncSettings value, CancellationToken cancellationToken = default) =>
        settings.SaveSyncSettingsAsync(value, cancellationToken);
}
