namespace HealthSyncy.iOS;

internal sealed class SummaryBL(IHealthReader reader) : ISummaryBL
{
    public async Task<string> Today()
    {
        await reader.Authorize();
        var now = DateTimeOffset.Now;
        var zone = TimeZoneInfo.Local;
        var day = DateOnly.FromDateTime(now.LocalDateTime);
        var totals = await reader.Read(day, zone);
        if (!totals.IsValid()) throw new InvalidOperationException("No readable activity yet.");
        var lines = new List<string> { $"Apple Health · {day:yyyy-MM-dd} · {now:HH:mm} ({zone.Id})" };
        void Add(string label, decimal? value, string unit)
        {
            if (value is not null) lines.Add($"{label}: {value:0.##}{unit}");
        }
        Add("Steps", totals.Steps, "");
        Add("Active energy", totals.ActiveKcal, " kcal");
        Add("Resting energy", totals.RestingKcal, " kcal");
        Add("Exercise", totals.ExerciseMinutes, " min");
        Add("Stand", totals.StandHours, " h");
        return string.Join(Environment.NewLine, lines);
    }
}
