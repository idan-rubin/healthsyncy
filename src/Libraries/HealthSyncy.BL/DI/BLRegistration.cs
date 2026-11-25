using HealthSyncy.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.BL.DI;

public static class BLRegistration
{
    public static IServiceCollection AddBL(this IServiceCollection services, IConfiguration config) =>
        services.AddActivityStore(config).AddSingleton<IActivityBL, ActivityBL>();
}
