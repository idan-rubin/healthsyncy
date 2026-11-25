using HealthSyncy.Mobile.Domain.Health;

namespace HealthSyncy.Mobile.UnitTests;

public sealed class BackgroundRegistrationTests
{
    private static readonly string[] _metrics = ["steps", "energy"];

    private readonly BackgroundRegistration _registration = new();
    private readonly List<string> _attempts = [];

    [Fact]
    public async Task Reports_the_metrics_that_could_not_be_enabled()
    {
        var unavailable = await _registration.EnsureAsync(_metrics, EnableAsync);

        unavailable.Should().Equal("energy");
    }

    [Fact]
    public async Task Retries_only_what_failed_even_when_calls_overlap()
    {
        await _registration.EnsureAsync(_metrics, EnableAsync);

        await Task.WhenAll(
            _registration.EnsureAsync(_metrics, EnableAsync),
            _registration.EnsureAsync(_metrics, EnableAsync));

        _attempts.Should().Equal("steps", "energy", "energy");
        (await _registration.EnsureAsync(_metrics, EnableAsync)).Should().BeEmpty();
    }

    private Task<bool> EnableAsync(string metric)
    {
        _attempts.Add(metric);
        return Task.FromResult(metric is "steps" || _attempts.Count(attempt => attempt is "energy") > 1);
    }
}
