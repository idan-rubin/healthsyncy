using HealthSyncy.Core;
using MongoDB.Driver;

var registration = new BackgroundRegistration();
var attempts = new List<string>();
Task<bool> Enable(string metric)
{
    attempts.Add(metric);
    return Task.FromResult(metric == "steps" || attempts.Count(x => x == metric) > 1);
}
string[] metrics = ["steps", "energy"];
Verify((await registration.Ensure(metrics, Enable)).SequenceEqual(["energy"]), "report failed registration");
await Task.WhenAll(registration.Ensure(metrics, Enable), registration.Ensure(metrics, Enable));
Verify(attempts.SequenceEqual(["steps", "energy", "energy"]), "retry only failed registrations");
Verify((await registration.Ensure(metrics, Enable)).Count == 0, "registration recovered");

var queue = new SyncQueue();
var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
int passes = 0;
async Task Refresh()
{
    if (Interlocked.Increment(ref passes) == 1)
    {
        started.SetResult();
        await finish.Task;
    }
}
var active = queue.Refresh(Refresh);
await started.Task;
var next = queue.Refresh(Refresh);
var joined = queue.Refresh(Refresh);
Verify(next == joined && !next.IsCompleted && passes == 1, "overlapping observers await one pending refresh");
finish.SetResult();
await Task.WhenAll(active, next, joined);
Verify(passes == 2, "refresh arriving during an upload triggers a second read");

var saved = "original";
try { await queue.Pair(() => Task.FromException<int>(new HttpRequestException("401")), () => saved = "rejected"); }
catch (HttpRequestException) { }
try { await queue.Pair(() => Task.FromResult(0), () => saved = "unverified"); }
catch (InvalidOperationException) { }
Verify(saved == "original", "failed or empty uploads preserve the working pairing");
await queue.Pair(() => Task.FromResult(1), () => saved = "accepted");
Verify(saved == "accepted", "accepted upload commits the replacement pairing");

var client = new MongoClient(Environment.GetEnvironmentVariable("TEST_MONGO_URI")
    ?? throw new InvalidOperationException("Set TEST_MONGO_URI to a disposable local MongoDB instance."));
var dbName = "healthsyncy_checks_" + Guid.NewGuid().ToString("N");
var store = new ActivityStore(client.GetDatabase(dbName));
const string zone = "America/New_York", day = "2026-11-01";
var at = new DateTimeOffset(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);
var data = new Upload(zone, at, new(1200, 66, 700, 13, 3, 500, 30, 12));
try
{
    Verify(Reporting.IsDay(day) && !Reporting.IsDay("2026-02-30"), "date validation");
    Verify(Reporting.TryZone(zone, out var tz) && Reporting.DayIn(at, tz!) == day, "local day");
    Verify(!Reporting.TryZone("invalid/timezone", out _), "unknown zone");
    Verify(!new DailyTotals(null,null,null,null,null,null,null,null).IsValid(), "missing observations");
    Verify(await store.Save(day, data, default) == WriteResult.Saved, "first insert");
    Verify(await store.Save(day, data, default) == WriteResult.Duplicate, "retry");
    Verify(await store.Save(day, data with { Totals = data.Totals with { Steps = 999 } }, default) == WriteResult.Conflict, "conflicting version");
    var corrections = Enumerable.Range(1,12).Select(i => store.Save(day, data with {
        CapturedAt = at.AddMinutes(i), Totals = data.Totals with { Steps = 900 - i } }, default));
    await Task.WhenAll(corrections);
    Verify(await store.Save(day, data, default) == WriteResult.Superseded, "late upload");
    Verify((await store.Get(day, zone, default))?.Totals.Steps == 888, "latest correction wins");
    var week = await store.Week(new(2026,11,1), zone, default);
    Verify(week.Count == 7 && week[0].Snapshot is null && week[6].Day == day, "weekly gaps");
    Verify(await store.Get(day,"UTC",default) is null, "zone isolation");
    await store.SetSettings(new(false,90),default);
    Verify(await store.Settings(default) == new SyncSettings(false,90), "sync controls");
    Console.WriteLine("Synthetic storage checks passed.");
}
finally { await client.DropDatabaseAsync(dbName); }

static void Verify(bool passed,string check)
{
    if (!passed) throw new InvalidOperationException(check);
}
