using HealthSyncy.Mobile.Domain.Pairings;

namespace HealthSyncy.Mobile.Domain.Services;

public interface ISyncService
{
    public event EventHandler<UploadedEventArgs>? Uploaded;

    public bool IsPaired { get; }

    public string? PairedOrigin { get; }

    public Task PairAsync(Pairing candidate);

    public Task RefreshAsync();

    public Task<IReadOnlyList<string>> ObserveAsync(Action<string> report);
}
