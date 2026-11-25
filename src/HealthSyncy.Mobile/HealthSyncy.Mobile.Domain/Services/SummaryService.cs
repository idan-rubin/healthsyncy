using System.Globalization;
using HealthSyncy.Mobile.Domain.Health;

namespace HealthSyncy.Mobile.Domain.Services;

internal sealed class SummaryService(IHealthReader reader, TimeProvider time) : ISummaryService
{
    public async Task<string> GetTodayAsync()
    {
        await reader.AuthorizeAsync();
        var now = time.GetLocalNow();
        var zone = time.LocalTimeZone;
        var day = DateOnly.FromDateTime(now.DateTime);
        var totals = await reader.ReadAsync(day, zone);
        if (!totals.IsValid())
            throw new SyncException("No readable activity yet.");

        (string Label, decimal? Value, string Unit)[] metrics =
        [
            ("Steps", totals.Steps, ""),
            ("Active energy", totals.ActiveKcal, " kcal"),
            ("Resting energy", totals.RestingKcal, " kcal"),
            ("Exercise", totals.ExerciseMinutes, " min"),
            ("Stand", totals.StandHours, " h")
        ];
        return string.Join(Environment.NewLine,
        [
            string.Create(CultureInfo.InvariantCulture, $"Apple Health · {day:yyyy-MM-dd} · {now:HH:mm} ({zone.Id})"),
            .. metrics.Where(metric => metric.Value is not null)
                .Select(metric => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{metric.Label}: {metric.Value:0.##}{metric.Unit}"))
        ]);
    }
}
