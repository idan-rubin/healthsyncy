using HealthSyncy.Core;

namespace HealthSyncy.iOS;

internal sealed class SyncBL(IHealthReader reader, ISyncServiceAgent agent, IPairingStore pairings) : ISyncBL
{
    private readonly SyncQueue uploads = new();

    public event Action<int>? Uploaded;
    public bool IsPaired => pairings.IsPaired;
    public string? PairedOrigin => pairings.Load()?.Origin;

    public async Task Pair(Pairing candidate)
    {
        await reader.Authorize();
        await uploads.Pair(() => UploadDays(candidate), () => pairings.Save(candidate));
    }

    public Task Refresh() => uploads.Refresh(() => UploadDays(
        pairings.Load() ?? throw new InvalidOperationException("Unlock your phone or pair it first.")));

    public Task<IReadOnlyList<string>> Observe(Action<string> report) => reader.Observe(Refresh, report);

    private async Task<int> UploadDays(Pairing pairing)
    {
        var zone = TimeZoneInfo.Local;
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var sent = 0;
        for (var offset = 0; offset < 7; offset++)
        {
            var day = today.AddDays(-offset);
            var totals = await reader.Read(day, zone);
            if (!totals.IsValid()) continue;
            await agent.Upload(pairing, day, new Upload(zone.Id, now, totals));
            sent++;
        }
        Uploaded?.Invoke(sent);
        return sent;
    }
}
