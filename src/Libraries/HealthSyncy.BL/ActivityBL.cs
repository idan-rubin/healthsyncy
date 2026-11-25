using System.Globalization;
using HealthSyncy.Core;

namespace HealthSyncy.BL;

public enum UploadResult { Saved, Duplicate, Superseded, Conflict, Invalid, Paused }
public enum SyncStatus { Current, Paused, AwaitingToday, Stale }
public sealed record SyncOverview(SyncSettings Settings, IReadOnlyList<DayRow> Days, SyncStatus Status);

public sealed class ActivityBL(ActivityStore store)
{
    public Task Ready(CancellationToken ct) => store.Ping(ct);

    public async Task<UploadResult> Accept(string day, Upload upload, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (!Reporting.TryZone(upload.TimeZone, out var zone) || upload.CapturedAt == default ||
            upload.CapturedAt > now.AddMinutes(5) ||
            string.CompareOrdinal(day, Reporting.DayIn(upload.CapturedAt, zone!)) > 0 ||
            upload.Totals is null || !upload.Totals.IsValid()) return UploadResult.Invalid;
        if (!(await store.Settings(ct)).Enabled) return UploadResult.Paused;
        var captured = DateTimeOffset.FromUnixTimeMilliseconds(upload.CapturedAt.ToUnixTimeMilliseconds()).UtcDateTime;
        var current = await store.SaveIfNewer(new(day, upload.TimeZone, captured, now.UtcDateTime, upload.Totals), ct);
        return current is null ? UploadResult.Saved :
            current.CapturedAtUtc > captured ? UploadResult.Superseded :
            current.Totals == upload.Totals ? UploadResult.Duplicate : UploadResult.Conflict;
    }

    public Task<DaySnapshot?> Day(string day, string zone, CancellationToken ct) => store.Get(day, zone, ct);

    public async Task<IReadOnlyList<DayRow>> Week(DateOnly end, string zone, CancellationToken ct)
    {
        var dates = Enumerable.Range(0, 7).Select(i => end.AddDays(i - 6).ToString("yyyy-MM-dd",
            CultureInfo.InvariantCulture)).ToArray();
        var found = (await store.GetMany(dates, zone, ct)).ToDictionary(snapshot => snapshot.Day);
        return dates.Select(day => new DayRow(day, found.GetValueOrDefault(day))).ToArray();
    }

    public async Task<SyncOverview> Overview(string zone, CancellationToken ct)
    {
        var settings = await store.Settings(ct);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, zone).DateTime);
        var days = await Week(today, zone, ct);
        var latest = days[^1].Snapshot;
        var status = !settings.Enabled ? SyncStatus.Paused :
            latest is null ? SyncStatus.AwaitingToday :
            now.UtcDateTime - latest.CapturedAtUtc > TimeSpan.FromMinutes(settings.StaleAfterMinutes) ? SyncStatus.Stale :
            SyncStatus.Current;
        return new(settings, days, status);
    }

    public Task SaveSettings(SyncSettings settings, CancellationToken ct) => store.SetSettings(settings, ct);
}
