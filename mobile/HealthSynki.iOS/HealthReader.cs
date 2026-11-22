using Foundation;
using HealthKit;
using HealthSynki.Core;

namespace HealthSynki.iOS;

internal sealed class HealthReader
{
    private readonly HKHealthStore store = new();
    private readonly HKQuantityType steps = HKQuantityType.Create(HKQuantityTypeIdentifier.StepCount)!;
    private readonly HKQuantityType resting = HKQuantityType.Create(HKQuantityTypeIdentifier.BasalEnergyBurned)!;
    private readonly HKQuantityType energy = HKQuantityType.Create(HKQuantityTypeIdentifier.ActiveEnergyBurned)!;
    private readonly List<HKObserverQuery> observers = [];

    public async Task Authorize()
    {
        if (!HKHealthStore.IsHealthDataAvailable) throw new InvalidOperationException("HealthKit is unavailable on this device.");
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reads = new NSSet(steps, resting, energy, HKObjectType.ActivitySummaryType);
        store.RequestAuthorizationToShare(null, reads, (ok, _) => done.TrySetResult(ok));
        if (!await done.Task) throw new InvalidOperationException("HealthKit authorization could not finish.");
    }

    public void Observe(Func<Task> sync)
    {
        if (observers.Count != 0) return;
        foreach (var type in new[] { steps, resting, energy })
        {
            var query = new HKObserverQuery(type, null, async (_, complete, error) =>
            {
                try { if (error is null) await sync(); }
                catch (Exception) { /* A later foreground or HealthKit wake retries the current window. */ }
                finally { complete(); }
            });
            observers.Add(query);
            store.ExecuteQuery(query);
            store.EnableBackgroundDelivery(type, HKUpdateFrequency.Hourly, (_, _) => { });
        }
    }

    public async Task<DailyTotals> Read(DateOnly day, TimeZoneInfo zone)
    {
        using var start = Midnight(day, zone);
        using var end = Midnight(day.AddDays(1), zone);
        using var range = HKQuery.GetPredicateForSamples(start, end, HKQueryOptions.None);
        var stepsTask = Total(steps, HKUnit.Count, start, range);
        var restingTask = Total(resting, HKUnit.Kilocalorie, start, range);
        var energyTask = Total(energy, HKUnit.Kilocalorie, start, range);
        var ringsTask = Rings(day, zone.Id);
        await Task.WhenAll(stepsTask, restingTask, energyTask, ringsTask);
        var rings = await ringsTask;
        bool moveEnergy = rings?.ActivityMoveMode == HKActivityMoveMode.ActiveEnergy;
        return new((int?)await stepsTask,
            moveEnergy ? (decimal)rings!.ActiveEnergyBurned.GetDoubleValue(HKUnit.Kilocalorie) : await energyTask,
            await restingTask, (int?)rings?.AppleExerciseTime.GetDoubleValue(HKUnit.Minute),
            (int?)rings?.AppleStandHours.GetDoubleValue(HKUnit.Count),
            moveEnergy ? (decimal?)rings!.ActiveEnergyBurnedGoal.GetDoubleValue(HKUnit.Kilocalorie) : null,
            (int?)rings?.ExerciseTimeGoal?.GetDoubleValue(HKUnit.Minute),
            (int?)rings?.StandHoursGoal?.GetDoubleValue(HKUnit.Count));
    }

    private static NSDate Midnight(DateOnly day, TimeZoneInfo zone)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(midnight)) midnight = midnight.AddMinutes(1);
        return (NSDate)TimeZoneInfo.ConvertTimeToUtc(midnight, zone);
    }

    private async Task<decimal?> Total(HKQuantityType type, HKUnit unit, NSDate start, NSPredicate range)
    {
        var done = new TaskCompletionSource<decimal?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var query = new HKStatisticsCollectionQuery(type, range, HKStatisticsOptions.CumulativeSum,
            start, new NSDateComponents { Day = 1 });
        query.InitialResultsHandler = (_, result, error) =>
        {
            if (ReadError(error, done)) return;
            var value = result?.GetStatistics(start)?.SumQuantity()?.GetDoubleValue(unit);
            done.TrySetResult((decimal?)value);
        };
        store.ExecuteQuery(query);
        try { return await done.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { store.StopQuery(query); }
    }

    private async Task<HKActivitySummary?> Rings(DateOnly day, string zone)
    {
        var calendar = new NSCalendar(NSCalendarType.Gregorian) { TimeZone = NSTimeZone.FromName(zone)! };
        var components = new NSDateComponents { Calendar = calendar, Era = 1, Year = day.Year, Month = day.Month, Day = day.Day };
        var done = new TaskCompletionSource<HKActivitySummary?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var query = new HKActivitySummaryQuery(HKQuery.GetPredicateForActivitySummary(components), (_, data, error) =>
        {
            if (!ReadError(error, done)) done.TrySetResult(data?.FirstOrDefault());
        });
        store.ExecuteQuery(query);
        try { return await done.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { store.StopQuery(query); }
    }

    private static bool ReadError<T>(NSError? error, TaskCompletionSource<T> done)
    {
        if (error is null) return false;
        if (error.Domain == "com.apple.healthkit" && error.Code == (long)HKErrorCode.NoData)
            done.TrySetResult(default!);
        else
            done.TrySetException(new InvalidOperationException("Health data is unavailable. Unlock the phone and retry."));
        return true;
    }
}
