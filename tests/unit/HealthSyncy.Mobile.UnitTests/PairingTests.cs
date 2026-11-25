using System.Text.Json;
using HealthSyncy.Mobile.Domain;
using HealthSyncy.Mobile.Domain.Pairings;

namespace HealthSyncy.Mobile.UnitTests;

public sealed class PairingTests
{
    [Theory]
    [InlineData("https://sync.example.org", "https://sync.example.org")]
    [InlineData(" https://sync.example.org/ ", "https://sync.example.org")]
    [InlineData("HTTPS://Sync.Example.org", "https://sync.example.org")]
    [InlineData("https://sync.example.org:8443", "https://sync.example.org:8443")]
    [InlineData("sync.example.org", "https://sync.example.org")]
    [InlineData(" sync.example.org:8443/ ", "https://sync.example.org:8443")]
    public void Keeps_the_origin_of_the_sync_server_address(string entered, string origin) =>
        Pairing.Create(entered, "device-token").Should().Be(new Pairing(origin, "device-token"));

    [Theory]
    [InlineData("http://sync.example.org")]
    [InlineData("https://sync.example.org/api")]
    [InlineData("https://sync.example.org/?debug=1")]
    [InlineData("https://sync.example.org/#top")]
    [InlineData("https://user:secret@sync.example.org")]
    [InlineData("sync example")]
    [InlineData("  ")]
    [InlineData("")]
    [InlineData(null)]
    public void Asks_for_the_sync_server_address_otherwise(string? address)
    {
        var pair = () => Pairing.Create(address, "device-token");

        pair.Should().Throw<SyncException>()
            .WithMessage("Enter the sync server address, such as https://sync.example.org.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Asks_for_the_device_token_when_it_is_missing(string? token)
    {
        var pair = () => Pairing.Create("https://sync.example.org", token);

        pair.Should().Throw<SyncException>().WithMessage("Paste the device upload token.");
    }

    [Fact]
    public void Drops_whitespace_pasted_around_the_token() =>
        Pairing.Create("sync.example.org", " device-token\n").Token.Should().Be("device-token");

    [Fact]
    public void Is_stored_as_origin_and_token()
    {
        var pairing = new Pairing("https://sync.example.org", "device-token");

        var stored = JsonSerializer.Serialize(pairing, MobileJson.Default.Pairing);

        stored.Should().Be("""{"origin":"https://sync.example.org","token":"device-token"}""");
        JsonSerializer.Deserialize(stored, MobileJson.Default.Pairing).Should().Be(pairing);
    }
}
