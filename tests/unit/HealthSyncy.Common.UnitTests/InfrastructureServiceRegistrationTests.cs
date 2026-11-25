using HealthSyncy.Common.Infrastructure.DI;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HealthSyncy.Common.UnitTests;

public sealed class InfrastructureServiceRegistrationTests
{
    [Fact]
    public void Maps_every_entity_repository_to_MongoDB()
    {
        using var services = Services(("MongoUri", "mongodb://127.0.0.1:1"));

        services.GetRequiredService<IRepository<SnapshotEntity>>().Should().BeOfType<MongoRepository<SnapshotEntity>>();
        services.GetRequiredService<IRepository<SettingsEntity>>().Should().BeOfType<MongoRepository<SettingsEntity>>();
    }

    [Fact]
    public void Uses_the_activity_database_unless_told_otherwise()
    {
        using var defaults = Services(("MongoUri", "mongodb://127.0.0.1:1"));
        using var custom = Services(("MongoUri", "mongodb://127.0.0.1:1"), ("MongoDatabase", "elsewhere"));

        defaults.GetRequiredService<IMongoDatabase>().DatabaseNamespace.DatabaseName.Should().Be("activity");
        custom.GetRequiredService<IMongoDatabase>().DatabaseNamespace.DatabaseName.Should().Be("elsewhere");
    }

    [Fact]
    public void Refuses_to_run_without_a_connection_string()
    {
        using var services = Services();

        var options = () => services.GetRequiredService<IOptions<MongoOptions>>().Value;

        options.Should().Throw<OptionsValidationException>().WithMessage("*MongoUri*required*");
    }

    [Fact]
    public void Registers_the_MongoDB_readiness_check_and_the_system_clock()
    {
        using var services = Services(("MongoUri", "mongodb://127.0.0.1:1"));

        services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Should().ContainSingle(check => check.Name == "mongodb");
        services.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
    }

    private static ServiceProvider Services(params (string Key, string Value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => KeyValuePair.Create(setting.Key, (string?)setting.Value)))
            .Build();
        return new ServiceCollection().AddLogging().AddHealthSyncyInfrastructure(config).BuildServiceProvider();
    }
}
