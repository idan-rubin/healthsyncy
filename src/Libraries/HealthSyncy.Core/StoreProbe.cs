using MongoDB.Bson;
using MongoDB.Driver;

namespace HealthSyncy.Core;

public sealed class StoreProbe(IMongoDatabase database) : IStoreProbe
{
    public Task Ping(CancellationToken ct) =>
        database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
}
