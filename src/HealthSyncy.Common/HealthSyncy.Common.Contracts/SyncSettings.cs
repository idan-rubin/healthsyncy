namespace HealthSyncy.Common.Contracts;

public sealed record SyncSettings(bool Enabled, int StaleAfterMinutes)
{
    public static SyncSettings Default { get; } = new(true, 180);
}
