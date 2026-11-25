using HealthSyncy.Common.Contracts;

namespace HealthSyncy.Mobile.Domain.Health;

public sealed record Activity(Metric Steps, Metric Active, Metric Exercise, Metric Stand, Metric Resting)
{
    public static Activity Unknown { get; } = Of(new(null, null, null, null, null, null, null, null));

    public Metric Burn => new("Burn so far", Active.Value + Resting.Value, "kcal");

    public IReadOnlyList<Metric> Metrics => [Steps, Active, Resting, Exercise, Stand];

    public static Activity Of(DailyTotals totals) => new(
        new("Steps", totals.Steps, ""),
        new("Active energy", totals.ActiveKcal, "kcal", totals.MoveGoalKcal),
        new("Exercise", totals.ExerciseMinutes, "min", totals.ExerciseGoalMinutes),
        new("Stand", totals.StandHours, "h", totals.StandGoalHours),
        new("Resting energy", totals.RestingKcal, "kcal"));
}
