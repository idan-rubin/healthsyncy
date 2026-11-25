using HealthSyncy.Mobile.Domain.ServiceAgents;
using HealthSyncy.Mobile.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.Mobile.Domain.DI;

public static class MobileDomainRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMobileDomain() => services
            .AddSingleton(TimeProvider.System)
            .AddSingleton(_ => new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CheckCertificateRevocationList = true
            })
            {
                Timeout = TimeSpan.FromSeconds(15)
            })
            .AddSingleton<ISyncServiceAgent, SyncServiceAgent>()
            .AddSingleton<ISyncService, SyncService>()
            .AddSingleton<ISummaryService, SummaryService>();
    }
}
