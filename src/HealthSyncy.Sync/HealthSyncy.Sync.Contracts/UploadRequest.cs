using HealthSyncy.Common.Contracts;

namespace HealthSyncy.Sync.Contracts;

public sealed record UploadRequest(string TimeZone, DateTimeOffset CapturedAt, DailyTotals Totals);
