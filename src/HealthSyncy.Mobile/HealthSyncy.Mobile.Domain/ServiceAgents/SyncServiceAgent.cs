using System.Net;
using System.Net.Http.Headers;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Sync.Contracts;

namespace HealthSyncy.Mobile.Domain.ServiceAgents;

internal sealed class SyncServiceAgent(HttpClient http) : ISyncServiceAgent
{
    public async Task UploadAsync(Pairing pairing, DateOnly day, UploadRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"{pairing.Origin}/api/v1/days/{day:yyyy-MM-dd}")
        {
            Content = JsonContent.Create(request, MobileJson.Default.UploadRequest),
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", pairing.Token) }
        };
        using var response = await http.SendAsync(message);
        if (response.StatusCode == HttpStatusCode.Locked)
            throw new SyncException("Uploads are paused in Admin.");
        if (!response.IsSuccessStatusCode)
            throw new SyncException($"Upload failed (HTTP {(int)response.StatusCode}).");
    }
}
