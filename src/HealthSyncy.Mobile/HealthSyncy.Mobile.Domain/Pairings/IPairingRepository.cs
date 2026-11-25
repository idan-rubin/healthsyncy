namespace HealthSyncy.Mobile.Domain.Pairings;

public interface IPairingRepository
{
    public bool IsPaired { get; }

    public Pairing? Load();

    public void Save(Pairing pairing);
}
