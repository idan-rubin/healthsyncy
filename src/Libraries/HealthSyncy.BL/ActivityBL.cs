using System.Globalization;
using HealthSyncy.Core;

namespace HealthSyncy.BL;

public sealed class ActivityBL(ISnapshotStore snapshots) : IActivityBL
{
    public Task<DaySnapshot?> Day(string day, string zone, CancellationToken ct) => snapshots.Get(day, zone, ct);

    public async Task<IReadOnlyList<DayRow>> Week(DateOnly end, string zone, CancellationToken ct)
    {
        var dates = Enumerable.Range(0, 7).Select(i => end.AddDays(i - 6).ToString("yyyy-MM-dd",
            CultureInfo.InvariantCulture)).ToArray();
        var found = (await snapshots.GetMany(dates, zone, ct)).ToDictionary(snapshot => snapshot.Day);
        return dates.Select(day => new DayRow(day, found.GetValueOrDefault(day))).ToArray();
    }
}
