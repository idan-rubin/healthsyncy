using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace HealthSyncy.Core;

public static class Configuration
{
    public static string Required(this IConfiguration config, string name) =>
        !string.IsNullOrWhiteSpace(config[name]) ? config[name]! :
            throw new InvalidOperationException($"Missing setting {name}.");

    public static string Https(this IConfiguration config, string name)
    {
        var value = config.Required(name);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException($"{name} needs an HTTPS URL.");
        return value;
    }

    public static IServiceCollection AddActivityStore(this IServiceCollection services, IConfiguration config)
    {
        var settings = MongoClientSettings.FromConnectionString(config.Required("MongoUri"));
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var database = new MongoClient(settings).GetDatabase("activity");
        services.AddSingleton<IActivityStore>(new ActivityStore(database));
        return services;
    }
}

public sealed class ActivityStore(IMongoDatabase database) : IActivityStore
{
    private sealed record SnapshotDocument(
        [property: BsonId] string Id, string Day, string TimeZone, DateTime CapturedAtUtc,
        DateTime ReceivedAtUtc, DailyTotals Totals)
    {
        public DaySnapshot ToSnapshot() => new(Day, TimeZone, CapturedAtUtc, ReceivedAtUtc, Totals);
    }
    private sealed record SettingsDocument([property: BsonId] string Id, bool Enabled, int StaleAfterMinutes);
    private readonly IMongoCollection<SnapshotDocument> days = database.GetCollection<SnapshotDocument>("days");
    private readonly IMongoCollection<SettingsDocument> settings = database.GetCollection<SettingsDocument>("settings");

    public Task Ping(CancellationToken ct) => database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
        new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: ct);

    public async Task<DaySnapshot?> SaveIfNewer(DaySnapshot snapshot, CancellationToken ct)
    {
        var id = $"{snapshot.TimeZone}|{snapshot.Day}";
        var captured = snapshot.CapturedAtUtc;
        var record = new SnapshotDocument(id, snapshot.Day, snapshot.TimeZone, captured,
            snapshot.ReceivedAtUtc, snapshot.Totals);
        while (true)
        {
            try
            {
                await days.ReplaceOneAsync(d => d.Id == id && d.CapturedAtUtc < captured,
                    record, new ReplaceOptions { IsUpsert = true }, ct);
                return null;
            }
            catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                var current = await days.Find(d => d.Id == id).SingleAsync(ct);
                if (current.CapturedAtUtc >= captured) return current.ToSnapshot();
            }
        }
    }

    public async Task<DaySnapshot?> Get(string day, string zone, CancellationToken ct) =>
        (await days.Find(d => d.Id == zone + "|" + day).SingleOrDefaultAsync(ct))?.ToSnapshot();

    public async Task<IReadOnlyList<DaySnapshot>> GetMany(IEnumerable<string> dates, string zone, CancellationToken ct)
    {
        var ids = dates.Select(day => $"{zone}|{day}").ToArray();
        var docs = await days.Find(Builders<SnapshotDocument>.Filter.In(d => d.Id, ids)).ToListAsync(ct);
        return docs.Select(d => d.ToSnapshot()).ToArray();
    }

    public async Task<SyncSettings> Settings(CancellationToken ct)
    {
        var value = await settings.Find(s => s.Id == "sync").FirstOrDefaultAsync(ct);
        return value is null ? SyncSettings.Default : new(value.Enabled, value.StaleAfterMinutes);
    }

    public Task SetSettings(SyncSettings value, CancellationToken ct) =>
        settings.ReplaceOneAsync(s => s.Id == "sync", new SettingsDocument("sync", value.Enabled,
            value.StaleAfterMinutes), new ReplaceOptions { IsUpsert = true }, ct);
}
