using HealthSyncy.Mobile.Domain.Health;

namespace HealthSyncy.Mobile.Domain.Services;

public interface ISummaryService
{
    public Task<Activity> ReadTodayAsync();

    public Task<string> GetTodayAsync();
}
