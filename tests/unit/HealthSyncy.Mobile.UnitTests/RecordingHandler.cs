using System.Net;

namespace HealthSyncy.Mobile.UnitTests;

internal sealed class RecordingHandler(HttpStatusCode status, string? reply = null) : HttpMessageHandler
{
    public HttpMethod? Method { get; private set; }

    public Uri? Address { get; private set; }

    public string? Authorization { get; private set; }

    public string? ContentType { get; private set; }

    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Method = request.Method;
        Address = request.RequestUri;
        Authorization = request.Headers.Authorization?.ToString();
        ContentType = request.Content?.Headers.ContentType?.ToString();
        Body = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken) : null;
        return new(status) { Content = reply is null ? null : new StringContent(reply) };
    }
}
