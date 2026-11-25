using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.iOS.DI;

internal static class MobileRegistration
{
    public static IServiceCollection AddMobile(this IServiceCollection services) => services
        .AddSingleton<IHealthReader, HealthReader>()
        .AddSingleton<IPairingStore, PairingStore>()
        .AddSingleton<ISyncServiceAgent, SyncServiceAgent>()
        .AddSingleton<ISyncBL, SyncBL>()
        .AddSingleton<ISummaryBL, SummaryBL>();
}
