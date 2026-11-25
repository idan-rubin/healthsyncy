using HealthSyncy.Common.Contracts;

namespace HealthSyncy.Mobile.Domain.Health;

public interface IHealthReader
{
    public Task AuthorizeAsync();

    public Task<IReadOnlyList<string>> ObserveAsync(Func<Task> onChange, Action<string> report);

    public Task<DailyTotals> ReadAsync(DateOnly day, TimeZoneInfo zone);
}
