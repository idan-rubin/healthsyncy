using HealthSyncy.Common.Contracts;

namespace HealthSyncy.Common.UnitTests;

public sealed class DailyTotalsTests
{
    private static readonly DailyTotals _plausible = new(1200, 66, 700, 13, 3, 500, 30, 12);

    [Fact]
    public void Plausible_totals_are_valid() => _plausible.IsValid().Should().BeTrue();

    [Fact]
    public void One_observation_is_enough() =>
        new DailyTotals(1, null, null, null, null, null, null, null).IsValid().Should().BeTrue();

    [Fact]
    public void Goals_without_an_observation_are_not_enough() =>
        new DailyTotals(null, null, null, null, null, 500, 30, 12).IsValid().Should().BeFalse();

    [Fact]
    public void Values_out_of_range_are_invalid()
    {
        DailyTotals[] outOfRange =
        [
            _plausible with { Steps = -1 },
            _plausible with { ActiveKcal = -0.1m },
            _plausible with { RestingKcal = -1 },
            _plausible with { ExerciseMinutes = -1 },
            _plausible with { ExerciseMinutes = 1501 },
            _plausible with { StandHours = -1 },
            _plausible with { StandHours = 26 },
            _plausible with { MoveGoalKcal = -1 },
            _plausible with { ExerciseGoalMinutes = -1 },
            _plausible with { StandGoalHours = -1 },
            _plausible with { StandGoalHours = 25 }
        ];

        outOfRange.Should().AllSatisfy(totals => totals.IsValid().Should().BeFalse());
    }

    [Fact]
    public void Values_at_the_limits_are_valid()
    {
        DailyTotals[] atTheLimits =
        [
            _plausible with { Steps = 0, ActiveKcal = 0, RestingKcal = 0 },
            _plausible with { ExerciseMinutes = 1500, StandHours = 25, StandGoalHours = 24 }
        ];

        atTheLimits.Should().AllSatisfy(totals => totals.IsValid().Should().BeTrue());
    }
}
