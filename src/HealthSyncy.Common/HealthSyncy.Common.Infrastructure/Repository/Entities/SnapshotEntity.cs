using HealthSyncy.Common.Contracts;
using MongoDB.Bson.Serialization.Attributes;

namespace HealthSyncy.Common.Infrastructure.Repository.Entities;

public sealed record SnapshotEntity(
    [property: BsonId] string Id,
    string Day,
    string TimeZone,
    DateTime CapturedAtUtc,
    DateTime ReceivedAtUtc,
    DailyTotals Totals) : IEntity
{
    public static string Collection => "days";

    public static string IdFor(string day, string zone) => $"{zone}|{day}";

    public DaySnapshot ToSnapshot() => new(Day, TimeZone, CapturedAtUtc, ReceivedAtUtc, Totals);
}
