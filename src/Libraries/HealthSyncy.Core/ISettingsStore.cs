namespace HealthSyncy.Core;

public interface ISettingsStore
{
    Task<SyncSettings> Get(CancellationToken ct);
    Task Set(SyncSettings value, CancellationToken ct);
}
