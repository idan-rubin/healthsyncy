using HealthSyncy.Common.Contracts;

namespace HealthSyncy.Admin.Contracts;

public sealed record SyncOverview(SyncSettings Settings, IReadOnlyList<DayRow> Days, SyncStatus Status);
