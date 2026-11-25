using System.Globalization;
using HealthSyncy.Common.Contracts;
using HealthSyncy.Mobile.Domain.Health;

namespace HealthSyncy.Mobile.Domain.Services;

internal sealed class SummaryService(IHealthReader reader, TimeProvider time) : ISummaryService
{
    public async Task<Activity> ReadTodayAsync() => Activity.Of((await ReadAsync()).Totals);

    public async Task<string> GetTodayAsync()
    {
        var (now, day, totals) = await ReadAsync();
        if (!totals.IsValid())
            throw new SyncException("No readable activity yet.");

        return string.Join(Environment.NewLine,
        [
            string.Create(CultureInfo.InvariantCulture,
                $"Apple Health · {day:yyyy-MM-dd} · {now:HH:mm} ({time.LocalTimeZone.Id})"),
            .. Activity.Of(totals).Metrics
                .Where(metric => metric.Value is not null)
                .Select(metric => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{metric.Label}: {metric.Value:0.##}{(metric.Unit.Length == 0 ? "" : " " + metric.Unit)}"))
        ]);
    }

    private async Task<(DateTimeOffset Now, DateOnly Day, DailyTotals Totals)> ReadAsync()
    {
        await reader.AuthorizeAsync();
        var now = time.GetLocalNow();
        var day = DateOnly.FromDateTime(now.DateTime);
        return (now, day, await reader.ReadAsync(day, time.LocalTimeZone));
    }
}
