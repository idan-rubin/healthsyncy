using HealthSyncy.Admin.Contracts;
using HealthSyncy.Common.Contracts;

namespace HealthSyncy.Admin.Domain;

public interface IAdminService
{
    public Task<SyncOverview> GetOverviewAsync(string zone, CancellationToken cancellationToken = default);
    public Task SaveSettingsAsync(SyncSettings value, CancellationToken cancellationToken = default);
}
