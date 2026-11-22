namespace HealthSynki.Core;

public sealed class BackgroundRegistration
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly HashSet<string> enabled = [];

    public async Task<IReadOnlyList<string>> Ensure(IEnumerable<string> metrics, Func<string, Task<bool>> enable)
    {
        await gate.WaitAsync();
        try
        {
            var unavailable = new List<string>();
            foreach (var metric in metrics)
            {
                if (enabled.Contains(metric)) continue;
                if (await enable(metric)) enabled.Add(metric);
                else unavailable.Add(metric);
            }
            return unavailable;
        }
        finally { gate.Release(); }
    }
}
