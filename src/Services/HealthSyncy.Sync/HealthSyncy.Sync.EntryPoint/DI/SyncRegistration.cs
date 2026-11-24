using System.Text.Json.Serialization;
using HealthSyncy.Core;

namespace HealthSyncy.Sync.EntryPoint.DI;

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
