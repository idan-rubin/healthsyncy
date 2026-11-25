using HealthSyncy.Common.Contracts;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;

namespace HealthSyncy.Common.UnitTests;

public sealed class SettingsQueriesTests
{
    private readonly AutoMocker _mocker = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Mock<IRepository<SettingsEntity>> Repository => _mocker.GetMock<IRepository<SettingsEntity>>();

    [Fact]
    public async Task Falls_back_to_the_defaults_until_settings_are_saved()
    {
        var settings = await Repository.Object.GetSyncSettingsAsync(Cancellation);

        settings.Should().Be(SyncSettings.Default).And.Be(new SyncSettings(true, 180));
    }

    [Fact]
    public async Task Reads_the_saved_settings()
    {
        Repository.Setup(repository => repository.GetByIdAsync("sync", Cancellation))
            .ReturnsAsync(new SettingsEntity("sync", false, 90));

        var settings = await Repository.Object.GetSyncSettingsAsync(Cancellation);

        settings.Should().Be(new SyncSettings(false, 90));
    }

    [Fact]
    public async Task Saves_settings_under_the_sync_id()
    {
        await Repository.Object.SaveSyncSettingsAsync(new SyncSettings(false, 90), Cancellation);

        Repository.Verify(repository => repository.UpsertAsync(new SettingsEntity("sync", false, 90), Cancellation));
    }
}
