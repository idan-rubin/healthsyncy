using System.Linq.Expressions;

namespace HealthSyncy.Common.Infrastructure.Repository;

public interface IRepository<T> where T : class, IEntity
{
    public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<T>> QueryAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    public Task UpsertAsync(T entity, CancellationToken cancellationToken = default);

    public Task<UpsertResult<T>> UpsertIfAsync(
        T entity,
        Expression<Func<T, bool>> canReplace,
        CancellationToken cancellationToken = default);
}
