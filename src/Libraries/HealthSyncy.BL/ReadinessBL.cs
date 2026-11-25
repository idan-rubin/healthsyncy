using HealthSyncy.Core;

namespace HealthSyncy.BL;

public sealed class ReadinessBL(IStoreProbe probe) : IReadinessBL
{
    public Task Ready(CancellationToken ct) => probe.Ping(ct);
}
