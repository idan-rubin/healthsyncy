using HealthSyncy.Core;

namespace HealthSyncy.iOS;

internal interface IHealthReader
{
    Task Authorize();
    Task<IReadOnlyList<string>> Observe(Func<Task> sync, Action<string> report);
    Task<DailyTotals> Read(DateOnly day, TimeZoneInfo zone);
}
