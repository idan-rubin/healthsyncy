using HealthSyncy.Common.Contracts;

namespace HealthSyncy.Connector.Domain;

public interface IActivityService
{
    public Task<DaySnapshot?> GetDayAsync(string day, string zone, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<DayRow>> GetWeekAsync(
        DateOnly endingOn,
        string zone,
        CancellationToken cancellationToken = default);
}
