using HealthKit;
using HealthSyncy.Common.Contracts;
using HealthSyncy.Mobile.Domain;
using HealthSyncy.Mobile.Domain.Health;

namespace HealthSyncy.Mobile.EntryPoint.Platform;

internal sealed class HealthReader : IHealthReader
{
    private static readonly TimeSpan _queryTimeout = TimeSpan.FromSeconds(15);

    private readonly HKHealthStore _store = new();
    private readonly HKQuantityType _steps = HKQuantityType.Create(HKQuantityTypeIdentifier.StepCount)!;
    private readonly HKQuantityType _resting = HKQuantityType.Create(HKQuantityTypeIdentifier.BasalEnergyBurned)!;
    private readonly HKQuantityType _energy = HKQuantityType.Create(HKQuantityTypeIdentifier.ActiveEnergyBurned)!;
    private readonly List<HKObserverQuery> _observers = [];
    private readonly BackgroundRegistration _registration = new();

    public async Task AuthorizeAsync()
    {
        if (!HKHealthStore.IsHealthDataAvailable)
            throw new SyncException("HealthKit is unavailable on this device.");
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reads = new NSSet(_steps, _resting, _energy, HKObjectType.ActivitySummaryType);
        _store.RequestAuthorizationToShare(null, reads, (ok, _) => done.TrySetResult(ok));
        if (!await done.Task)
            throw new SyncException("HealthKit authorization could not finish.");
    }

    public Task<IReadOnlyList<string>> ObserveAsync(Func<Task> onChange, Action<string> report)
    {
        Dictionary<string, HKQuantityType> metrics = new(StringComparer.Ordinal)
        {
            ["steps"] = _steps,
            ["resting energy"] = _resting,
            ["active energy"] = _energy
        };
        if (_observers.Count == 0)
        {
            foreach (var (name, type) in metrics)
            {
                var query = new HKObserverQuery(type, null,
                    (observer, complete, error) => _ = DeliverAsync(name, error, complete, onChange, report));
                _observers.Add(query);
                _store.ExecuteQuery(query);
            }
        }
        return _registration.EnsureAsync(metrics.Keys, name =>
        {
            var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _store.EnableBackgroundDelivery(metrics[name], HKUpdateFrequency.Hourly,
                (ok, error) => result.TrySetResult(ok && error is null));
            return result.Task.WaitAsync(_queryTimeout);
        });
    }

    public async Task<DailyTotals> ReadAsync(DateOnly day, TimeZoneInfo zone)
    {
        using var start = Midnight(day, zone);
        using var end = Midnight(day.AddDays(1), zone);
        using var range = HKQuery.GetPredicateForSamples(start, end, HKQueryOptions.None);
        var stepsTask = TotalAsync(_steps, HKUnit.Count, start, range);
        var restingTask = TotalAsync(_resting, HKUnit.Kilocalorie, start, range);
        var energyTask = TotalAsync(_energy, HKUnit.Kilocalorie, start, range);
        var ringsTask = RingsAsync(day, zone.Id);
        await Task.WhenAll(stepsTask, restingTask, energyTask, ringsTask);
        var rings = await ringsTask;
        var moveEnergy = rings?.ActivityMoveMode == HKActivityMoveMode.ActiveEnergy;
        return new(
            Steps: (int?)await stepsTask,
            ActiveKcal: moveEnergy
                ? (decimal)rings!.ActiveEnergyBurned.GetDoubleValue(HKUnit.Kilocalorie)
                : await energyTask,
            RestingKcal: await restingTask,
            ExerciseMinutes: (int?)rings?.AppleExerciseTime.GetDoubleValue(HKUnit.Minute),
            StandHours: (int?)rings?.AppleStandHours.GetDoubleValue(HKUnit.Count),
            MoveGoalKcal: moveEnergy
                ? (decimal?)rings!.ActiveEnergyBurnedGoal.GetDoubleValue(HKUnit.Kilocalorie)
                : null,
            ExerciseGoalMinutes: (int?)rings?.ExerciseTimeGoal?.GetDoubleValue(HKUnit.Minute),
            StandGoalHours: (int?)rings?.StandHoursGoal?.GetDoubleValue(HKUnit.Count));
    }

    private static async Task DeliverAsync(string metric, NSError? error, Action complete, Func<Task> onChange,
        Action<string> report)
    {
        try
        {
            if (error is null)
                await onChange();
            else
                report($"HealthKit could not deliver {metric} ({error.Code}).");
        }
        catch (Exception failure)
        {
            report(failure is SyncException ? failure.Message : "Background upload failed. Retry with Sync now.");
        }
        finally
        {
            complete();
        }
    }

    private static NSDate Midnight(DateOnly day, TimeZoneInfo zone)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(midnight))
            midnight = midnight.AddMinutes(1);
        return (NSDate)TimeZoneInfo.ConvertTimeToUtc(midnight, zone);
    }

    private static void Complete<T>(TaskCompletionSource<T> done, NSError? error, Func<T> read)
    {
        if (error is null)
            _ = done.TrySetResult(read());
        else if (error.Domain is "com.apple.healthkit" && error.Code == (long)HKErrorCode.NoData)
            _ = done.TrySetResult(default!);
        else
            _ = done.TrySetException(new SyncException("Health data is unavailable. Unlock the phone and retry."));
    }

    private async Task<decimal?> TotalAsync(HKQuantityType type, HKUnit unit, NSDate start, NSPredicate range)
    {
        var done = new TaskCompletionSource<decimal?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var query = new HKStatisticsCollectionQuery(type, range, HKStatisticsOptions.CumulativeSum,
            start, new NSDateComponents { Day = 1 });
        query.InitialResultsHandler = (_, result, error) => Complete(done, error,
            () => (decimal?)result?.GetStatistics(start)?.SumQuantity()?.GetDoubleValue(unit));
        _store.ExecuteQuery(query);
        try
        {
            return await done.Task.WaitAsync(_queryTimeout);
        }
        finally
        {
            _store.StopQuery(query);
        }
    }

    private async Task<HKActivitySummary?> RingsAsync(DateOnly day, string zone)
    {
        var calendar = new NSCalendar(NSCalendarType.Gregorian) { TimeZone = NSTimeZone.FromName(zone)! };
        var components = new NSDateComponents
        {
            Calendar = calendar,
            Era = 1,
            Year = day.Year,
            Month = day.Month,
            Day = day.Day
        };
        var done = new TaskCompletionSource<HKActivitySummary?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var query = new HKActivitySummaryQuery(HKQuery.GetPredicateForActivitySummary(components),
            (_, data, error) => Complete(done, error, () => data?.FirstOrDefault()));
        _store.ExecuteQuery(query);
        try
        {
            return await done.Task.WaitAsync(_queryTimeout);
        }
        finally
        {
            _store.StopQuery(query);
        }
    }
}
