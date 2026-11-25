namespace HealthSyncy.Core;

// Serialize uploads and pairing changes. Refreshes waiting for the same next
// pass share its completion, including any error reported to their observers.
public sealed class SyncQueue
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly object state = new();
    private TaskCompletionSource? pending;

    public Task Refresh(Func<Task> upload)
    {
        TaskCompletionSource batch;
        lock (state)
        {
            if (pending is not null) return pending.Task;
            pending = batch = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _ = RunRefresh(batch, upload);
        return batch.Task;
    }

    private async Task RunRefresh(TaskCompletionSource batch, Func<Task> upload)
    {
        await gate.WaitAsync();
        try
        {
            lock (state) pending = null;
            await upload();
            batch.SetResult();
        }
        catch (Exception error) { batch.SetException(error); }
        finally { gate.Release(); }
    }

    public async Task Pair(Func<Task<int>> uploadCandidate, Action save)
    {
        await gate.WaitAsync();
        try
        {
            if (await uploadCandidate() == 0)
                throw new InvalidOperationException("No readable activity to verify the pairing. Try again when activity is available.");
            save();
        }
        finally { gate.Release(); }
    }
}
