using HealthSyncy.Common.Infrastructure.Health;
using HealthSyncy.Common.Infrastructure.Repository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HealthSyncy.Common.Infrastructure.DI;

public static class InfrastructureServiceRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddHealthSyncyInfrastructure(IConfiguration config)
        {
            _ = services.AddOptions<MongoOptions>().Bind(config).ValidateDataAnnotations().ValidateOnStart();
            _ = services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");
            services.TryAddSingleton(TimeProvider.System);
            return services
                .AddSingleton(provider =>
                {
                    var options = provider.GetRequiredService<IOptions<MongoOptions>>().Value;
                    var settings = MongoClientSettings.FromConnectionString(options.MongoUri);
                    settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
                    return new MongoClient(settings).GetDatabase(options.MongoDatabase);
                })
                .AddSingleton(typeof(IRepository<>), typeof(MongoRepository<>));
        }
    }
}
