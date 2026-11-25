namespace HealthSyncy.Common.Infrastructure.Repository;

public readonly record struct UpsertResult<T>(bool Written, T Stored) where T : class, IEntity;
