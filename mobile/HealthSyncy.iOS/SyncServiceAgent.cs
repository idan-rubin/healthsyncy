using System.Net.Http.Headers;
using System.Net.Http.Json;
using HealthSyncy.Core;

namespace HealthSyncy.iOS;

internal sealed class SyncServiceAgent : ISyncServiceAgent
{
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };

    public async Task Upload(Pairing pairing, DateOnly day, Upload upload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            FormattableString.Invariant($"{pairing.Origin}/api/v1/days/{day:yyyy-MM-dd}"))
            { Content = JsonContent.Create(upload, MobileJson.Default.Upload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pairing.Token);
        using var response = await http.SendAsync(request);
        if ((int)response.StatusCode == 423) throw new InvalidOperationException("Uploads are paused in Admin.");
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Upload failed (HTTP {(int)response.StatusCode}).");
    }
}
