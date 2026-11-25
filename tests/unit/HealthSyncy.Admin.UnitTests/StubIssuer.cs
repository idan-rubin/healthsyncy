using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace HealthSyncy.Admin.UnitTests;

internal sealed class StubIssuer : HttpMessageHandler
{
    public string IdToken { get; set; } = "";

    public Uri? TokenEndpoint { get; private set; }

    public IReadOnlyDictionary<string, string> TokenRequest { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        TokenEndpoint = request.RequestUri;
        TokenRequest = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken))
            .ToDictionary(field => field.Key, field => field.Value.ToString(), StringComparer.Ordinal);
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id_token":"{{IdToken}}","access_token":"access-token","token_type":"Bearer"}""",
                Encoding.UTF8, "application/json")
        };
    }
}
