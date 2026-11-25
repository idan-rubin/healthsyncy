namespace HealthSyncy.Mobile.Domain.Health;

public sealed record Metric(string Label, decimal? Value, string Unit, decimal? Goal = null)
{
    public decimal? Progress => Value is { } value && Goal is > 0m ? Math.Min(value / Goal.Value, 1m) : null;
}
