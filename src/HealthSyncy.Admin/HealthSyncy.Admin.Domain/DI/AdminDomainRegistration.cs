using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.Admin.Domain.DI;

public static class AdminDomainRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAdminDomain() => services.AddSingleton<IAdminService, AdminService>();
    }
}
