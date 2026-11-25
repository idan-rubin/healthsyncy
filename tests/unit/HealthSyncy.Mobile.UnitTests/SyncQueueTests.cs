using HealthSyncy.Mobile.Domain;
using HealthSyncy.Mobile.Domain.Services;

namespace HealthSyncy.Mobile.UnitTests;

public sealed class SyncQueueTests
{
    private readonly SyncQueue _queue = new();

    [Fact]
    public async Task Refreshes_arriving_during_an_upload_share_one_more_pass()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var passes = 0;

        async Task UploadAsync()
        {
            if (Interlocked.Increment(ref passes) == 1)
            {
                started.SetResult();
                await finish.Task;
            }
        }

        var active = _queue.RefreshAsync(UploadAsync);
        await started.Task;
        var next = _queue.RefreshAsync(UploadAsync);
        var joined = _queue.RefreshAsync(UploadAsync);

        joined.Should().BeSameAs(next);
        next.IsCompleted.Should().BeFalse();
        passes.Should().Be(1);

        finish.SetResult();
        await Task.WhenAll(active, next, joined);

        passes.Should().Be(2);
    }

    [Fact]
    public async Task A_failed_pass_reaches_its_callers_and_the_next_refresh_starts_clean()
    {
        var passes = 0;
        var failing = () => _queue.RefreshAsync(() => throw new SyncException("Upload failed (HTTP 500)."));

        await failing.Should().ThrowAsync<SyncException>().WithMessage("Upload failed (HTTP 500).");
        await _queue.RefreshAsync(() =>
        {
            passes++;
            return Task.CompletedTask;
        });

        passes.Should().Be(1);
    }

    [Fact]
    public async Task Saves_the_pairing_once_an_upload_is_accepted()
    {
        var saved = "original";

        await _queue.PairAsync(() => Task.FromResult(1), () => saved = "accepted");

        saved.Should().Be("accepted");
    }

    [Fact]
    public async Task Keeps_the_working_pairing_when_the_candidate_is_rejected()
    {
        var saved = "original";
        var pair = () => _queue.PairAsync(
            () => Task.FromException<int>(new SyncException("Upload failed (HTTP 401).")), () => saved = "rejected");

        await pair.Should().ThrowAsync<SyncException>().WithMessage("Upload failed (HTTP 401).");
        saved.Should().Be("original");
    }

    [Fact]
    public async Task Keeps_the_working_pairing_when_there_is_no_activity_to_verify_with()
    {
        var saved = "original";
        var pair = () => _queue.PairAsync(() => Task.FromResult(0), () => saved = "unverified");

        await pair.Should().ThrowAsync<SyncException>()
            .WithMessage("No readable activity to verify the pairing. Try again when activity is available.");
        saved.Should().Be("original");
    }

    [Fact]
    public async Task Pairing_waits_for_an_upload_that_is_already_running()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paired = false;
        var refresh = _queue.RefreshAsync(async () =>
        {
            started.SetResult();
            await finish.Task;
        });
        await started.Task;

        var pairing = _queue.PairAsync(() => Task.FromResult(1), () => paired = true);

        paired.Should().BeFalse();

        finish.SetResult();
        await Task.WhenAll(refresh, pairing);

        paired.Should().BeTrue();
    }
}
