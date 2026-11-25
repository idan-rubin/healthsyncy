namespace HealthSyncy.BL;

public interface IReadinessBL
{
    Task Ready(CancellationToken ct);
}
