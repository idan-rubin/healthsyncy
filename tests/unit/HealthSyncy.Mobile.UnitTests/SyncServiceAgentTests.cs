using System.Globalization;
using System.Net;
using HealthSyncy.Mobile.Domain;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Mobile.Domain.ServiceAgents;
using HealthSyncy.Sync.Contracts;

namespace HealthSyncy.Mobile.UnitTests;

public sealed class SyncServiceAgentTests
{
    private static readonly Pairing _pairing = new("https://sync.example.org", "device-token");

    private static readonly UploadRequest _upload = new("UTC", new(2026, 11, 1, 18, 0, 0, TimeSpan.Zero),
        new(1200, 66.5m, 700, 13, 3, 500, 30, 12));

    [Theory]
    [InlineData("en-US")]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    public async Task Puts_the_day_to_the_paired_origin_with_the_device_token(string phoneCulture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(phoneCulture);
        using var handler = new RecordingHandler(HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        var agent = new SyncServiceAgent(http);

        await agent.UploadAsync(_pairing, new(2026, 11, 1), _upload);

        handler.Method.Should().Be(HttpMethod.Put);
        handler.Address.Should().Be(new Uri("https://sync.example.org/api/v1/days/2026-11-01"));
        handler.Authorization.Should().Be("Bearer device-token");
        handler.ContentType.Should().Be("application/json; charset=utf-8");
        handler.Body.Should().Be(
            """{"timeZone":"UTC","capturedAt":"2026-11-01T18:00:00+00:00","totals":{"steps":1200,"activeKcal":66.5,"restingKcal":700,"exerciseMinutes":13,"standHours":3,"moveGoalKcal":500,"exerciseGoalMinutes":30,"standGoalHours":12}}""");
    }

    [Theory]
    [InlineData(HttpStatusCode.Locked, null, "Uploads are paused in Admin.")]
    [InlineData(HttpStatusCode.Unauthorized, null,
        "The sync server did not accept the device token. Pair again with the current token.")]
    [InlineData(HttpStatusCode.BadRequest, "The capture time is missing or in the future.\n",
        "The sync server rejected the upload: The capture time is missing or in the future.")]
    [InlineData(HttpStatusCode.Conflict, "Conflicting snapshot at the same capture time.",
        "The sync server rejected the upload: Conflicting snapshot at the same capture time.")]
    [InlineData(HttpStatusCode.BadRequest, null, "Upload failed (HTTP 400).")]
    [InlineData(HttpStatusCode.Redirect, null, "Upload failed (HTTP 302).")]
    [InlineData(HttpStatusCode.BadGateway, "Bad Gateway", "Upload failed (HTTP 502).")]
    public async Task Explains_an_upload_the_server_did_not_accept(HttpStatusCode status, string? reply, string message)
    {
        using var handler = new RecordingHandler(status, reply);
        using var http = new HttpClient(handler);
        var agent = new SyncServiceAgent(http);

        var upload = () => agent.UploadAsync(_pairing, new(2026, 11, 1), _upload);

        await upload.Should().ThrowAsync<SyncException>().WithMessage(message);
    }
}
