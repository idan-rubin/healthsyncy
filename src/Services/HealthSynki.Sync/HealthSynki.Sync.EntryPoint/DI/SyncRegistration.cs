using System.Text.Json.Serialization;
using HealthSynki.Core;

namespace HealthSynki.Sync.EntryPoint.DI;

internal static class SyncRegistration
{
    public static IServiceCollection AddSync(this IServiceCollection services, IConfiguration config)
    {
        services.AddActivityStore(config);
        services.AddControllers().AddJsonOptions(o =>
            o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
        return services;
    }
}
