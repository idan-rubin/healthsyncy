namespace HealthSyncy.Mobile.Domain.Services;

public interface ISummaryService
{
    public Task<string> GetTodayAsync();
}
