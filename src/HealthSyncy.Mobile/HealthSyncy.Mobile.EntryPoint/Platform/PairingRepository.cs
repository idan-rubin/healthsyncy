using System.Text.Json;
using HealthSyncy.Mobile.Domain;
using HealthSyncy.Mobile.Domain.Pairings;
using Security;

namespace HealthSyncy.Mobile.EntryPoint.Platform;

internal sealed class PairingRepository : IPairingRepository
{
    private const string PairedKey = "paired";

    public bool IsPaired => NSUserDefaults.StandardUserDefaults.BoolForKey(PairedKey);

    public Pairing? Load()
    {
        using var query = Lookup();
        using var data = SecKeyChain.QueryAsData(query, false, out var status);
        return status == SecStatusCode.Success && data is not null
            ? JsonSerializer.Deserialize(data.ToArray(), MobileJson.Default.Pairing)
            : null;
    }

    public void Save(Pairing pairing)
    {
        using var query = Lookup();
        using var data = NSData.FromArray(JsonSerializer.SerializeToUtf8Bytes(pairing, MobileJson.Default.Pairing));
        using var value = new SecRecord { ValueData = data, Accessible = SecAccessible.WhenUnlockedThisDeviceOnly };
        var result = SecKeyChain.Update(query, value);
        if (result == SecStatusCode.ItemNotFound)
        {
            query.ValueData = data;
            query.Accessible = SecAccessible.WhenUnlockedThisDeviceOnly;
            result = SecKeyChain.Add(query);
        }
        if (result != SecStatusCode.Success)
            throw new SyncException("Could not save device pairing.");
        NSUserDefaults.StandardUserDefaults.SetBool(true, PairedKey);
    }

    private static SecRecord Lookup() =>
        new(SecKind.GenericPassword) { Service = "HealthSyncy.Sync", Account = "paired-device" };
}
