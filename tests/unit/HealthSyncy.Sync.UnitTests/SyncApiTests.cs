using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using HealthSyncy.Sync.Contracts;
using HealthSyncy.Sync.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HealthSyncy.Sync.UnitTests;

public sealed class SyncApiTests : IDisposable
{
    private const string DeviceToken = "device-token";

    private const string Upload = """
        {"timeZone":"UTC","capturedAt":"2026-11-01T18:00:00Z","totals":{"steps":123,"activeKcal":20.5}}
        """;

    private static readonly UploadRequest _upload = new("UTC", new(2026, 11, 1, 18, 0, 0, TimeSpan.Zero),
        new(123, 20.5m, null, null, null, null, null, null));

    private readonly Mock<IUploadService> _uploads = new();
    private readonly WebApplicationFactory<Program> _factory;

    public SyncApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host
            .UseSetting("MongoUri", "mongodb://127.0.0.1:1")
            .UseSetting("UploadTokenSha256", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DeviceToken))))
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureTestServices(services => services
                .AddSingleton(_uploads.Object)
                .AddSingleton(Mock.Of<IMongoDatabase>())));
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Saves_an_upload_from_the_paired_phone()
    {
        Outcome(new Accepted(UploadStatus.Saved));

        using var response = await PutAsync("2026-11-01", Upload, DeviceToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Cancellation)).Should().Be("""{"status":"Saved"}""");
        _uploads.Verify(uploads => uploads.AcceptAsync("2026-11-01", _upload, It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(UploadStatus.Duplicate, """{"status":"Duplicate"}""")]
    [InlineData(UploadStatus.Superseded, """{"status":"Superseded"}""")]
    public async Task Names_the_status_of_an_accepted_upload(UploadStatus status, string expected)
    {
        Outcome(new Accepted(status));

        using var response = await PutAsync("2026-11-01", Upload, DeviceToken);

        (await response.Content.ReadAsStringAsync(Cancellation)).Should().Be(expected);
    }

    [Fact]
    public async Task Explains_why_an_upload_was_rejected()
    {
        Outcome(new Rejected("Use an IANA time zone."));

        using var response = await PutAsync("2026-11-01", Upload, DeviceToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Cancellation)).Should().Be("Use an IANA time zone.");
    }

    [Fact]
    public async Task Answers_locked_while_uploads_are_paused()
    {
        Outcome(new Paused());

        using var response = await PutAsync("2026-11-01", Upload, DeviceToken);

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
    }

    [Fact]
    public async Task Answers_conflict_for_a_different_snapshot_at_the_same_capture_time()
    {
        Outcome(new Conflicting());

        using var response = await PutAsync("2026-11-01", Upload, DeviceToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(Cancellation)).Should()
            .Be("Conflicting snapshot at the same capture time.");
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("today")]
    public async Task Rejects_a_day_that_is_not_a_calendar_date(string day)
    {
        using var response = await PutAsync(day, Upload, DeviceToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Cancellation)).Should().Be("Use a YYYY-MM-DD day.");
        _uploads.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("""{"timeZone":"UTC"}""")]
    [InlineData("""{"timeZone":"UTC","capturedAt":"2026-11-01T18:00:00Z","totals":{"steps":1},"extra":true}""")]
    [InlineData("not json")]
    public async Task Rejects_a_body_that_does_not_match_the_contract(string body)
    {
        using var response = await PutAsync("2026-11-01", body, DeviceToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _uploads.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("another-token")]
    public async Task Turns_away_callers_without_the_device_token(string? token)
    {
        using var response = await PutAsync("2026-11-01", Upload, token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle().Which.Scheme.Should().Be("Bearer");
        _uploads.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    public async Task Reports_health_without_a_token(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Cancellation);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Cancellation)).Should().Be("Healthy");
    }

    [Fact]
    public async Task Reports_not_ready_while_MongoDB_is_unreachable()
    {
        using var offline = _factory.WithWebHostBuilder(host => host
            .ConfigureTestServices(services => services.AddSingleton(UnreachableDatabase())));
        using var client = offline.CreateClient();

        using var ready = await client.GetAsync(new Uri("/readyz", UriKind.Relative), Cancellation);
        using var live = await client.GetAsync(new Uri("/healthz", UriKind.Relative), Cancellation);

        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        live.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Refuses_to_start_with_a_malformed_token_digest()
    {
        using var misconfigured = _factory.WithWebHostBuilder(host => host.UseSetting("UploadTokenSha256", "secret"));

        var start = () => misconfigured.CreateClient();

        start.Should().Throw<OptionsValidationException>()
            .WithMessage("*UploadTokenSha256 needs a SHA-256 digest in hex.*");
    }

    private void Outcome(UploadOutcome outcome) => _uploads
        .Setup(uploads => uploads.AcceptAsync(It.IsAny<string>(), It.IsAny<UploadRequest>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(outcome);

    private async Task<HttpResponseMessage> PutAsync(string day, string body, string? token)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/v1/days/{day}", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, Cancellation);
    }

    private static IMongoDatabase UnreachableDatabase()
    {
        var database = new Mock<IMongoDatabase>();
        database.Setup(mongo => mongo.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(), It.IsAny<ReadPreference>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("No server available."));
        return database.Object;
    }
}
