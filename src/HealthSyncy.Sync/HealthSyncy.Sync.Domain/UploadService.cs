using HealthSyncy.Common.Contracts;
using HealthSyncy.Common.Infrastructure.Repository;
using HealthSyncy.Common.Infrastructure.Repository.Entities;
using HealthSyncy.Sync.Contracts;

namespace HealthSyncy.Sync.Domain;

internal sealed class UploadService(
    IRepository<SnapshotEntity> snapshots,
    IRepository<SettingsEntity> settings,
    TimeProvider time) : IUploadService
{
    private static readonly TimeSpan _clockTolerance = TimeSpan.FromMinutes(5);

    public async Task<UploadOutcome> AcceptAsync(
        string day,
        UploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        if (Objection(day, request, now) is { } reason)
            return new Rejected(reason);
        if (!(await settings.GetSyncSettingsAsync(cancellationToken)).Enabled)
            return new Paused();

        var captured = DateTimeOffset.FromUnixTimeMilliseconds(request.CapturedAt.ToUnixTimeMilliseconds()).UtcDateTime;
        var candidate = new SnapshotEntity(SnapshotEntity.IdFor(day, request.TimeZone), day, request.TimeZone,
            captured, now.UtcDateTime, request.Totals);
        var result = await snapshots.UpsertIfAsync(
            candidate,
            stored => stored.CapturedAtUtc < captured,
            cancellationToken);
        return result switch
        {
            { Written: true } => new Accepted(UploadStatus.Saved),
            { Stored.CapturedAtUtc: var newer } when newer > captured => new Accepted(UploadStatus.Superseded),
            { Stored.Totals: var totals } when totals == request.Totals => new Accepted(UploadStatus.Duplicate),
            _ => new Conflicting()
        };
    }

    private static string? Objection(string day, UploadRequest request, DateTimeOffset now)
    {
        if (!Reporting.IsZone(request.TimeZone))
            return "Use an IANA time zone.";
        if (request.CapturedAt == default || request.CapturedAt > now + _clockTolerance)
            return "The capture time is missing or in the future.";
        if (string.CompareOrdinal(day, Reporting.DayIn(request.CapturedAt, request.TimeZone)) > 0)
            return "The day is later than the capture time.";
        return request.Totals?.IsValid() is true ? null : "The totals are missing or out of range.";
    }
}
