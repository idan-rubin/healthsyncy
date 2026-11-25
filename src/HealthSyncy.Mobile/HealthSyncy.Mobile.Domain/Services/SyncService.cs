using HealthSyncy.Mobile.Domain.Health;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Mobile.Domain.ServiceAgents;

namespace HealthSyncy.Mobile.Domain.Services;

internal sealed class SyncService(
    IHealthReader reader,
    ISyncServiceAgent agent,
    IPairingRepository pairings,
    TimeProvider time) : ISyncService
{
    private const int DaysPerPass = 7;

    private readonly SyncQueue _uploads = new();

    public event EventHandler<UploadedEventArgs>? Uploaded;

    public bool IsPaired => pairings.IsPaired;

    public string? PairedOrigin => pairings.Load()?.Origin;

    public async Task PairAsync(Pairing candidate)
    {
        await reader.AuthorizeAsync();
        await _uploads.PairAsync(() => UploadWeekAsync(candidate), () => pairings.Save(candidate));
    }

    public Task RefreshAsync() => _uploads.RefreshAsync(() => UploadWeekAsync(
        pairings.Load() ?? throw new SyncException("Unlock your phone or pair it first.")));

    public Task<IReadOnlyList<string>> ObserveAsync(Action<string> report) => reader.ObserveAsync(RefreshAsync, report);

    private async Task<int> UploadWeekAsync(Pairing pairing)
    {
        var zone = time.LocalTimeZone;
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var sent = 0;
        foreach (var day in Enumerable.Range(0, DaysPerPass).Select(offset => today.AddDays(-offset)))
        {
            var totals = await reader.ReadAsync(day, zone);
            if (!totals.IsValid())
                continue;
            await agent.UploadAsync(pairing, day, new(zone.Id, now, totals));
            sent++;
        }
        Uploaded?.Invoke(this, new(sent));
        return sent;
    }
}
