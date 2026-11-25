using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace HealthSyncy.Core.DI;

public static class CoreRegistration
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration config)
    {
        var settings = MongoClientSettings.FromConnectionString(config.Required("MongoUri"));
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        return services.AddSingleton(new MongoClient(settings).GetDatabase("activity"))
            .AddSingleton<ISnapshotStore, SnapshotStore>()
            .AddSingleton<ISettingsStore, SettingsStore>()
            .AddSingleton<IStoreProbe, StoreProbe>();
    }
}
