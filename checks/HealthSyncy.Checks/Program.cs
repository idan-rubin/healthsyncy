using HealthSyncy.Admin.Contracts;
using HealthSyncy.Admin.Domain;
using HealthSyncy.Admin.Domain.DI;
using HealthSyncy.Common.Infrastructure.DI;
using HealthSyncy.Connector.Domain;
using HealthSyncy.Connector.Domain.DI;
using HealthSyncy.Sync.Contracts;
using HealthSyncy.Sync.Domain;
using HealthSyncy.Sync.Domain.DI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

const string zone = "America/New_York";
const string day = "2026-11-01";

var databaseName = $"healthsyncy_checks_{Guid.NewGuid():N}";
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["MongoUri"] = Environment.GetEnvironmentVariable("TEST_MONGO_URI")
            ?? throw new InvalidOperationException("Set TEST_MONGO_URI to a disposable local MongoDB instance."),
        ["MongoDatabase"] = databaseName
    })
    .Build();
await using var services = new ServiceCollection()
    .AddLogging()
    .AddHealthSyncyInfrastructure(config)
    .AddSyncDomain()
    .AddConnectorDomain()
    .AddAdminDomain()
    .BuildServiceProvider();
var uploads = services.GetRequiredService<IUploadService>();
var activity = services.GetRequiredService<IActivityService>();
var admin = services.GetRequiredService<IAdminService>();

var capturedAt = new DateTimeOffset(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);
var upload = new UploadRequest(zone, capturedAt, new(1200, 66, 700, 13, 3, 500, 30, 12));
try
{
    Verify(await uploads.AcceptAsync(day, upload) is Accepted { Status: UploadStatus.Saved }, "first insert");
    Verify(await uploads.AcceptAsync(day, upload) is Accepted { Status: UploadStatus.Duplicate }, "retry");
    var conflicting = upload with { Totals = upload.Totals with { Steps = 999 } };
    Verify(await uploads.AcceptAsync(day, conflicting) is Conflicting, "conflicting version");

    var corrections = Enumerable.Range(1, 12).Select(minute => uploads.AcceptAsync(day, upload with
    {
        CapturedAt = capturedAt.AddMinutes(minute),
        Totals = upload.Totals with { Steps = 900 - minute }
    }));
    _ = await Task.WhenAll(corrections);
    Verify(await uploads.AcceptAsync(day, upload) is Accepted { Status: UploadStatus.Superseded }, "late upload");
    Verify((await activity.GetDayAsync(day, zone))?.Totals.Steps == 888, "latest correction wins");

    var week = await activity.GetWeekAsync(new(2026, 11, 1), zone);
    Verify(week is [{ Snapshot: null }, .., { Day: day, Snapshot: not null }] && week.Count == 7, "weekly gaps");
    Verify(await activity.GetDayAsync(day, "UTC") is null, "zone isolation");

    Verify((await admin.GetOverviewAsync("UTC")).Status == SyncStatus.AwaitingToday, "missing snapshot for today");
    await admin.SaveSettingsAsync(new(false, 90));
    Verify(await admin.GetOverviewAsync(zone) is { Settings: (false, 90), Status: SyncStatus.Paused }, "sync controls");
    Verify(await uploads.AcceptAsync(day, upload) is Paused, "paused uploads");
    Console.WriteLine("Synthetic storage checks passed.");
}
finally
{
    await services.GetRequiredService<IMongoDatabase>().Client.DropDatabaseAsync(databaseName);
}

static void Verify(bool passed, string check)
{
    if (!passed)
        throw new InvalidOperationException(check);
}
