using HealthSyncy.Common.Contracts;
using HealthSyncy.Common.Infrastructure.Repository.Entities;

namespace HealthSyncy.Common.Infrastructure.Repository;

public static class SettingsQueries
{
    extension(IRepository<SettingsEntity> settings)
    {
        public async Task<SyncSettings> GetSyncSettingsAsync(CancellationToken cancellationToken = default)
        {
            var saved = await settings.GetByIdAsync(SettingsEntity.SyncId, cancellationToken);
            return saved?.ToSettings() ?? SyncSettings.Default;
        }

        public Task SaveSyncSettingsAsync(SyncSettings value, CancellationToken cancellationToken = default) =>
            settings.UpsertAsync(SettingsEntity.From(value), cancellationToken);
    }
}
