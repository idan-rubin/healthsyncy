using System.Globalization;

namespace HealthSynki.Core;

public sealed record DailyTotals(
    int? Steps,
    decimal? ActiveKcal,
    decimal? RestingKcal,
    int? ExerciseMinutes,
    int? StandHours,
    decimal? MoveGoalKcal,
    int? ExerciseGoalMinutes,
    int? StandGoalHours)
{
    public bool IsValid() =>
        (Steps.HasValue || ActiveKcal.HasValue || RestingKcal.HasValue ||
         ExerciseMinutes.HasValue || StandHours.HasValue) &&
        Steps is not < 0 && ActiveKcal is not < 0 && RestingKcal is not < 0 &&
        ExerciseMinutes is not (< 0 or > 1500) && StandHours is not (< 0 or > 25) &&
        MoveGoalKcal is not < 0 && ExerciseGoalMinutes is not < 0 && StandGoalHours is not (< 0 or > 24);
}

public sealed record Upload(string TimeZone, DateTimeOffset CapturedAt, DailyTotals Totals);
public sealed record DaySnapshot(string Day, string TimeZone, DateTime CapturedAtUtc,
    DateTime ReceivedAtUtc, DailyTotals Totals);
public sealed record SyncSettings(bool Enabled, int StaleAfterMinutes)
{
    public static SyncSettings Default { get; } = new(true, 180);
}
public sealed record DayRow(string Day, DaySnapshot? Snapshot);

public static class Reporting
{
    public static bool IsDay(string? day) => DateOnly.TryParseExact(day, "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static bool TryZone(string? id, out TimeZoneInfo? zone)
    {
        zone = null;
        return !string.IsNullOrWhiteSpace(id) && id.Length <= 100 &&
            TimeZoneInfo.TryFindSystemTimeZoneById(id, out zone) && (zone.HasIanaId || id == "UTC");
    }

    public static string DayIn(DateTimeOffset instant, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
