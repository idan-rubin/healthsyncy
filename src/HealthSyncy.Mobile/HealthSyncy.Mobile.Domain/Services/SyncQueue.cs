namespace HealthSyncy.Mobile.Domain.Services;

internal sealed class SyncQueue
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Lock _state = new();
    private TaskCompletionSource? _pending;

    public Task RefreshAsync(Func<Task> uploadAsync)
    {
        TaskCompletionSource batch;
        lock (_state)
        {
            if (_pending is not null)
                return _pending.Task;
            _pending = batch = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _ = RunRefreshAsync(batch, uploadAsync);
        return batch.Task;
    }

    public async Task PairAsync(Func<Task<int>> uploadCandidateAsync, Action save)
    {
        await _gate.WaitAsync();
        try
        {
            if (await uploadCandidateAsync() == 0)
            {
                throw new SyncException(
                    "No readable activity to verify the pairing. Try again when activity is available.");
            }
            save();
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async Task RunRefreshAsync(TaskCompletionSource batch, Func<Task> uploadAsync)
    {
        await _gate.WaitAsync();
        try
        {
            lock (_state)
                _pending = null;
            await uploadAsync();
            batch.SetResult();
        }
        catch (Exception error)
        {
            batch.SetException(error);
        }
        finally
        {
            _ = _gate.Release();
        }
    }
}
