using System.Globalization;
using HealthSyncy.Common.Contracts;
using HealthSyncy.Mobile.Domain.Health;

namespace HealthSyncy.Mobile.UnitTests;

public sealed class ActivityTests
{
    private static readonly Activity _activity = Activity.Of(new DailyTotals(7240, 386.4m, 1650, 32, 9, 500, 30, 12));

    [Fact]
    public void Names_each_metric_with_its_unit_and_goal() =>
        _activity.Should().Be(new Activity(
            new("Steps", 7240, ""),
            new("Active energy", 386.4m, "kcal", 500),
            new("Exercise", 32, "min", 30),
            new("Stand", 9, "h", 12),
            new("Resting energy", 1650, "kcal")));

    [Fact]
    public void Adds_active_and_resting_energy_into_the_burn_so_far() =>
        _activity.Burn.Should().Be(new Metric("Burn so far", 2036.4m, "kcal"));

    [Theory]
    [InlineData(386, null)]
    [InlineData(null, 1650)]
    [InlineData(null, null)]
    public void Leaves_the_burn_unknown_until_both_energies_are_readable(int? active, int? resting) =>
        Activity.Of(new DailyTotals(7240, active, resting, null, null, null, null, null)).Burn.Value.Should().BeNull();

    [Fact]
    public void Lists_the_metrics_in_the_order_the_summary_shares_them() =>
        _activity.Metrics.Select(metric => metric.Label).Should().Equal(
            "Steps", "Active energy", "Resting energy", "Burn so far", "Exercise", "Stand");

    [Theory]
    [InlineData(386, 500, "0.772")]
    [InlineData(32, 30, "1")]
    [InlineData(0, 12, "0")]
    public void Measures_progress_towards_the_goal_up_to_complete(int value, int goal, string progress) =>
        new Metric("Exercise", value, "min", goal).Progress.Should().Be(decimal.Parse(progress, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData(null, 30)]
    [InlineData(32, null)]
    [InlineData(32, 0)]
    public void Has_no_progress_without_a_value_and_a_goal(int? value, int? goal) =>
        new Metric("Exercise", value, "min", goal).Progress.Should().BeNull();

    [Fact]
    public void Starts_with_every_metric_unknown() =>
        Activity.Unknown.Metrics.Should().OnlyContain(metric => metric.Value == null && metric.Goal == null);
}
