using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.Connector.Domain.DI;

public static class ConnectorDomainRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddConnectorDomain() => services.AddSingleton<IActivityService, ActivityService>();
    }
}
