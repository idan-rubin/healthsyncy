using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace HealthSyncy.Core;

public sealed class SnapshotStore(IMongoDatabase database) : ISnapshotStore
{
    private sealed record SnapshotDocument(
        [property: BsonId] string Id, string Day, string TimeZone, DateTime CapturedAtUtc,
        DateTime ReceivedAtUtc, DailyTotals Totals)
    {
        public DaySnapshot ToSnapshot() => new(Day, TimeZone, CapturedAtUtc, ReceivedAtUtc, Totals);
    }
    private readonly IMongoCollection<SnapshotDocument> days = database.GetCollection<SnapshotDocument>("days");

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
}
