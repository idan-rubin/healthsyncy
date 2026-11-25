using System.Globalization;

namespace HealthSyncy.Common.Contracts;

public static class Reporting
{
    private const string DayFormat = "yyyy-MM-dd";

    public static bool IsDay(string? day) => DateOnly.TryParseExact(day, DayFormat,
        CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static bool IsZone(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 100 &&
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && (zone.HasIanaId || id is "UTC");

    public static string DayIn(DateTimeOffset instant, string zone) =>
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(instant, zone).ToString(DayFormat, CultureInfo.InvariantCulture);

    public static IReadOnlyList<string> WeekEnding(DateOnly end) =>
        [.. Enumerable.Range(0, 7).Select(i => end.AddDays(i - 6).ToString(DayFormat, CultureInfo.InvariantCulture))];
}
