namespace HealthSyncy.iOS;

internal interface IPairingStore
{
    bool IsPaired { get; }
    Pairing? Load();
    void Save(Pairing pairing);
}
