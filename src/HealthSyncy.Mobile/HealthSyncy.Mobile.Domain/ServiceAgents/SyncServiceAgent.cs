using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Sync.Contracts;

namespace HealthSyncy.Mobile.Domain.ServiceAgents;

internal sealed class SyncServiceAgent(HttpClient http) : ISyncServiceAgent
{
    public async Task UploadAsync(Pairing pairing, DateOnly day, UploadRequest request)
    {
        var address = string.Create(CultureInfo.InvariantCulture, $"{pairing.Origin}/api/v1/days/{day:yyyy-MM-dd}");
        using var message = new HttpRequestMessage(HttpMethod.Put, address)
        {
            Content = JsonContent.Create(request, MobileJson.Default.UploadRequest),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", pairing.Token) }
        };
        using var response = await http.SendAsync(message);
        if (!response.IsSuccessStatusCode)
            throw new SyncException(await ExplainAsync(response));
    }

    private static async Task<string> ExplainAsync(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.Locked => "Uploads are paused in Admin.",
        HttpStatusCode.Unauthorized =>
            "The sync server did not accept the device token. Pair again with the current token.",
        var status when (int)status is >= 400 and < 500 && response.Content.Headers.ContentType?.MediaType is "text/plain" =>
            $"The sync server rejected the upload: {(await response.Content.ReadAsStringAsync()).Trim()}",
        var status => string.Create(CultureInfo.InvariantCulture, $"Upload failed (HTTP {(int)status}).")
    };
}
