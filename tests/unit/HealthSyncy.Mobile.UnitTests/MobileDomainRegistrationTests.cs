using HealthSyncy.Mobile.Domain.DI;
using HealthSyncy.Mobile.Domain.Health;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Mobile.Domain.ServiceAgents;
using HealthSyncy.Mobile.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.Mobile.UnitTests;

public sealed class MobileDomainRegistrationTests
{
    [Fact]
    public void Maps_the_phones_services_to_their_implementations()
    {
        using var services = Services();

        services.GetRequiredService<ISyncService>().Should().BeOfType<SyncService>();
        services.GetRequiredService<ISummaryService>().Should().BeOfType<SummaryService>();
        services.GetRequiredService<ISyncServiceAgent>().Should().BeOfType<SyncServiceAgent>();
        services.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
    }

    [Fact]
    public void Gives_an_upload_fifteen_seconds()
    {
        using var services = Services();

        services.GetRequiredService<HttpClient>().Timeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    private static ServiceProvider Services() => new ServiceCollection()
        .AddMobileDomain()
        .AddSingleton(Mock.Of<IHealthReader>())
        .AddSingleton(Mock.Of<IPairingRepository>())
        .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
