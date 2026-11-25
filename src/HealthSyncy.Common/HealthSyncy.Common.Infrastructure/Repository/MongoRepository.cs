using System.Linq.Expressions;
using MongoDB.Driver;

namespace HealthSyncy.Common.Infrastructure.Repository;

internal sealed class MongoRepository<T>(IMongoDatabase database) : IRepository<T> where T : class, IEntity
{
    private static readonly ReplaceOptions _upsert = new() { IsUpsert = true };

    private readonly IMongoCollection<T> _collection = database.GetCollection<T>(T.Collection);

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _collection.Find(ById(id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> QueryAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) =>
        await _collection.Find(predicate).ToListAsync(cancellationToken);

    public Task UpsertAsync(T entity, CancellationToken cancellationToken = default) =>
        _collection.ReplaceOneAsync(ById(entity.Id), entity, _upsert, cancellationToken);

    public async Task<UpsertResult<T>> UpsertIfAsync(
        T entity,
        Expression<Func<T, bool>> canReplace,
        CancellationToken cancellationToken = default)
    {
        var replaceable = ById(entity.Id) & Builders<T>.Filter.Where(canReplace);
        while (true)
        {
            try
            {
                _ = await _collection.ReplaceOneAsync(replaceable, entity, _upsert, cancellationToken);
                return new(true, entity);
            }
            catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                var stored = await _collection.Find(ById(entity.Id)).SingleAsync(cancellationToken);
                if (!canReplace.Compile()(stored))
                    return new(false, stored);
            }
        }
    }

    private static FilterDefinition<T> ById(string id) => Builders<T>.Filter.Eq("_id", id);
}
