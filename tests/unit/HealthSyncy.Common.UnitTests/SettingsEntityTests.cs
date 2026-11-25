using HealthSyncy.Common.Contracts;
using HealthSyncy.Common.Infrastructure.Repository.Entities;

namespace HealthSyncy.Common.UnitTests;

public sealed class SettingsEntityTests
{
    [Fact]
    public void Lives_in_the_settings_collection() => SettingsEntity.Collection.Should().Be("settings");

    [Fact]
    public void Stores_the_sync_settings_under_one_id() =>
        SettingsEntity.From(new SyncSettings(false, 90)).Should().Be(new SettingsEntity("sync", false, 90));

    [Fact]
    public void Maps_to_the_settings_contract() =>
        new SettingsEntity("sync", true, 45).ToSettings().Should().Be(new SyncSettings(true, 45));
}
