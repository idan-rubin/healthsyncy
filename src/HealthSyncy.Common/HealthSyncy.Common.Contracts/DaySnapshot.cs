namespace HealthSyncy.Common.Contracts;

public sealed record DaySnapshot(string Day, string TimeZone, DateTime CapturedAtUtc,
    DateTime ReceivedAtUtc, DailyTotals Totals);
