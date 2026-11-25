using HealthSyncy.Core.DI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.BL.DI;

public static class BLRegistration
{
    public static IServiceCollection AddBL(this IServiceCollection services, IConfiguration config) =>
        services.AddStorage(config)
            .AddSingleton<IReadinessBL, ReadinessBL>()
            .AddSingleton<IUploadBL, UploadBL>()
            .AddSingleton<IActivityBL, ActivityBL>()
            .AddSingleton<IAdminBL, AdminBL>();
}
