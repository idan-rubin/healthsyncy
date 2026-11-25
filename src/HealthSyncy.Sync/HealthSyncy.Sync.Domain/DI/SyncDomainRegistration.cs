using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.Sync.Domain.DI;

public static class SyncDomainRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSyncDomain() => services.AddSingleton<IUploadService, UploadService>();
    }
}
