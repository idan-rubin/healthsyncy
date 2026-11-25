using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace HealthSyncy.Core;

public sealed class SettingsStore(IMongoDatabase database) : ISettingsStore
{
    private sealed record SettingsDocument([property: BsonId] string Id, bool Enabled, int StaleAfterMinutes);
    private readonly IMongoCollection<SettingsDocument> settings = database.GetCollection<SettingsDocument>("settings");

    public async Task<SyncSettings> Get(CancellationToken ct)
    {
        var value = await settings.Find(s => s.Id == "sync").FirstOrDefaultAsync(ct);
        return value is null ? SyncSettings.Default : new(value.Enabled, value.StaleAfterMinutes);
    }

    public Task Set(SyncSettings value, CancellationToken ct) =>
        settings.ReplaceOneAsync(s => s.Id == "sync", new SettingsDocument("sync", value.Enabled,
            value.StaleAfterMinutes), new ReplaceOptions { IsUpsert = true }, ct);
}
