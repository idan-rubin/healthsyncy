using HealthSyncy.Core;

namespace HealthSyncy.BL;

public sealed class UploadBL(ISnapshotStore snapshots, ISettingsStore settings) : IUploadBL
{
    public async Task<UploadResult> Accept(string day, Upload upload, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (!Reporting.TryZone(upload.TimeZone, out var zone) || upload.CapturedAt == default ||
            upload.CapturedAt > now.AddMinutes(5) ||
            string.CompareOrdinal(day, Reporting.DayIn(upload.CapturedAt, zone!)) > 0 ||
            upload.Totals is null || !upload.Totals.IsValid()) return UploadResult.Invalid;
        if (!(await settings.Get(ct)).Enabled) return UploadResult.Paused;
        var captured = DateTimeOffset.FromUnixTimeMilliseconds(upload.CapturedAt.ToUnixTimeMilliseconds()).UtcDateTime;
        var current = await snapshots.SaveIfNewer(new(day, upload.TimeZone, captured, now.UtcDateTime, upload.Totals), ct);
        return current is null ? UploadResult.Saved :
            current.CapturedAtUtc > captured ? UploadResult.Superseded :
            current.Totals == upload.Totals ? UploadResult.Duplicate : UploadResult.Conflict;
    }
}
