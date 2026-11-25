using HealthSyncy.Common.Contracts;
using MongoDB.Bson.Serialization.Attributes;

namespace HealthSyncy.Common.Infrastructure.Repository.Entities;

public sealed record SettingsEntity([property: BsonId] string Id, bool Enabled, int StaleAfterMinutes) : IEntity
{
    public const string SyncId = "sync";

    public static string Collection => "settings";

    public static SettingsEntity From(SyncSettings settings) =>
        new(SyncId, settings.Enabled, settings.StaleAfterMinutes);

    public SyncSettings ToSettings() => new(Enabled, StaleAfterMinutes);
}
