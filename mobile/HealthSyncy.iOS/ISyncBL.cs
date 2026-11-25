namespace HealthSyncy.iOS;

internal interface ISyncBL
{
    event Action<int>? Uploaded;
    bool IsPaired { get; }
    string? PairedOrigin { get; }
    Task Pair(Pairing candidate);
    Task Refresh();
    Task<IReadOnlyList<string>> Observe(Action<string> report);
}
