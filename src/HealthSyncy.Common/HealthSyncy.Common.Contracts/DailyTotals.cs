namespace HealthSyncy.Common.Contracts;

public sealed record DailyTotals(
    int? Steps,
    decimal? ActiveKcal,
    decimal? RestingKcal,
    int? ExerciseMinutes,
    int? StandHours,
    decimal? MoveGoalKcal,
    int? ExerciseGoalMinutes,
    int? StandGoalHours)
{
    public bool IsValid() =>
        (Steps.HasValue || ActiveKcal.HasValue || RestingKcal.HasValue ||
         ExerciseMinutes.HasValue || StandHours.HasValue) &&
        Steps is not < 0 && ActiveKcal is not < 0 && RestingKcal is not < 0 &&
        ExerciseMinutes is not (< 0 or > 1500) && StandHours is not (< 0 or > 25) &&
        MoveGoalKcal is not < 0 && ExerciseGoalMinutes is not < 0 && StandGoalHours is not (< 0 or > 24);
}
