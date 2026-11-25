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
    public void Keeps_the_origin_of_an_HTTPS_address(string entered, string origin) =>
        Pairing.Create(entered, "device-token").Should().Be(new Pairing(origin, "device-token"));

    [Theory]
    [InlineData("http://sync.example.org", "device-token")]
    [InlineData("https://sync.example.org/api", "device-token")]
    [InlineData("https://sync.example.org/?debug=1", "device-token")]
    [InlineData("https://sync.example.org/#top", "device-token")]
    [InlineData("https://user:secret@sync.example.org", "device-token")]
    [InlineData("sync.example.org", "device-token")]
    [InlineData("", "device-token")]
    [InlineData(null, "device-token")]
    [InlineData("https://sync.example.org", "")]
    [InlineData("https://sync.example.org", "  ")]
    [InlineData("https://sync.example.org", null)]
    public void Asks_for_an_HTTPS_origin_and_a_token_otherwise(string? origin, string? token)
    {
        var pair = () => Pairing.Create(origin, token);

        pair.Should().Throw<SyncException>().WithMessage("Enter an HTTPS origin and device token.");
    }

    [Fact]
    public void Is_stored_as_origin_and_token()
    {
        var pairing = new Pairing("https://sync.example.org", "device-token");

        var stored = JsonSerializer.Serialize(pairing, MobileJson.Default.Pairing);

        stored.Should().Be("""{"origin":"https://sync.example.org","token":"device-token"}""");
        JsonSerializer.Deserialize(stored, MobileJson.Default.Pairing).Should().Be(pairing);
    }
}
