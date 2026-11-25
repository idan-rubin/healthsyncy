using HealthSyncy.Mobile.Domain.DI;
using HealthSyncy.Mobile.Domain.Health;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Mobile.EntryPoint.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.Mobile.EntryPoint.DI;

internal static class MobileServiceRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMobile() => services
            .AddMobileDomain()
            .AddSingleton<IHealthReader, HealthReader>()
            .AddSingleton<IPairingRepository, PairingRepository>();
    }
}
