namespace HealthSyncy.Core;

public interface IStoreProbe
{
    Task Ping(CancellationToken ct);
}
