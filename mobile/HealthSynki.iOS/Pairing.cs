using System.Text.Json;
using Foundation;
using Security;

namespace HealthSynki.iOS;

internal sealed record Pairing(string Origin, string Token)
{
    private static SecRecord Lookup() => new(SecKind.GenericPassword)
        { Service = "HealthSynki.Sync", Account = "paired-device" };

    public static Pairing? Load()
    {
        using var query = Lookup();
        using var data = SecKeyChain.QueryAsData(query, false, out var status);
        return status == SecStatusCode.Success && data is not null
            ? JsonSerializer.Deserialize(data.ToArray(), MobileJson.Default.Pairing) : null;
    }

    public void Save()
    {
        using var query = Lookup();
        using var data = NSData.FromArray(JsonSerializer.SerializeToUtf8Bytes(this, MobileJson.Default.Pairing));
        using var value = new SecRecord { ValueData = data, Accessible = SecAccessible.WhenUnlockedThisDeviceOnly };
        var result = SecKeyChain.Update(query, value);
        if (result == SecStatusCode.ItemNotFound)
        {
            query.ValueData = data;
            query.Accessible = SecAccessible.WhenUnlockedThisDeviceOnly;
            result = SecKeyChain.Add(query);
        }
        if (result != SecStatusCode.Success) throw new InvalidOperationException("Could not save device pairing.");
    }
}
