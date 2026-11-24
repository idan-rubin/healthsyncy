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
        services.AddSingleton(new ActivityStore(database));
        return services;
    }
}

public enum WriteResult { Saved, Duplicate, Superseded, Conflict }

public sealed class ActivityStore(IMongoDatabase database)
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

    public async Task<WriteResult> Save(string day, Upload upload, CancellationToken ct)
    {
        var id = $"{upload.TimeZone}|{day}";
        var stamp = DateTimeOffset.FromUnixTimeMilliseconds(upload.CapturedAt.ToUnixTimeMilliseconds()).UtcDateTime;
        var record = new SnapshotDocument(id, day, upload.TimeZone, stamp, DateTime.UtcNow, upload.Totals);
        while (true)
        {
            try
            {
                await days.ReplaceOneAsync(d => d.Id == id && d.CapturedAtUtc < stamp,
                    record, new ReplaceOptions { IsUpsert = true }, ct);
                return WriteResult.Saved;
            }
            catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                var current = await days.Find(d => d.Id == id).SingleAsync(ct);
                if (current.CapturedAtUtc < stamp) continue;
                if (current.CapturedAtUtc > stamp) return WriteResult.Superseded;
                return current.Totals == upload.Totals ? WriteResult.Duplicate : WriteResult.Conflict;
            }
        }
    }

    public async Task<DaySnapshot?> Get(string day, string zone, CancellationToken ct) =>
        (await days.Find(d => d.Id == zone + "|" + day).SingleOrDefaultAsync(ct))?.ToSnapshot();

    public async Task<IReadOnlyList<DayRow>> Week(DateOnly end, string zone, CancellationToken ct)
    {
        var dates = Enumerable.Range(0, 7).Select(i => end.AddDays(i - 6).ToString("yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var ids = dates.Select(day => $"{zone}|{day}").ToArray();
        var docs = await days.Find(Builders<SnapshotDocument>.Filter.In(d => d.Id, ids)).ToListAsync(ct);
        var byDay = docs.ToDictionary(d => d.Day);
        return dates.Select(day => new DayRow(day, byDay.GetValueOrDefault(day)?.ToSnapshot())).ToArray();
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
