using HealthSyncy.Core;

namespace HealthSyncy.BL;

public interface IActivityBL
{
    Task<DaySnapshot?> Day(string day, string zone, CancellationToken ct);
    Task<IReadOnlyList<DayRow>> Week(DateOnly end, string zone, CancellationToken ct);
}
