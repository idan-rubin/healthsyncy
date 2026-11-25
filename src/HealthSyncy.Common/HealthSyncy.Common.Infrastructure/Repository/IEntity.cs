namespace HealthSyncy.Common.Infrastructure.Repository;

public interface IEntity
{
    public static abstract string Collection { get; }

    public string Id { get; }
}
