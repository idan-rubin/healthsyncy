namespace HealthSyncy.Mobile.Domain.Health;

public sealed class BackgroundRegistration
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly HashSet<string> _enabled = [with(StringComparer.Ordinal)];

    public async Task<IReadOnlyList<string>> EnsureAsync(
        IEnumerable<string> metrics,
        Func<string, Task<bool>> enableAsync)
    {
        await _gate.WaitAsync();
        try
        {
            List<string> unavailable = [];
            foreach (var metric in metrics)
            {
                if (_enabled.Contains(metric))
                    continue;
                if (await enableAsync(metric))
                    _enabled.Add(metric);
                else
                    unavailable.Add(metric);
            }
            return unavailable;
        }
        finally
        {
            _ = _gate.Release();
        }
    }
}
