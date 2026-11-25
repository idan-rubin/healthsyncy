using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using HealthSyncy.Common.Contracts;
using HealthSyncy.Connector.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace HealthSyncy.Connector.UnitTests;

public sealed class ConnectorApiTests : IDisposable
{
    private const string Issuer = "https://issuer.test/realms/health";
    private const string Resource = "https://connector.test/mcp";
    private const string Owner = "owner-subject";
    private const string DayPath = "/api/v1/activity/2026-11-01?timeZone=UTC";

    private static readonly SymmetricSecurityKey _signingKey = new(RandomNumberGenerator.GetBytes(32));

    private static readonly DaySnapshot _snapshot = new("2026-11-01", "UTC",
        new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 18, 1, 0, DateTimeKind.Utc),
        new(1200, 66.5m, 700, 13, 3, 500, 30, 12));

    private readonly Mock<IActivityService> _activity = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ConnectorApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host
            .UseSetting("MongoUri", "mongodb://127.0.0.1:1")
            .UseSetting("OAuthIssuer", Issuer)
            .UseSetting("McpResource", Resource)
            .UseSetting("OwnerSubject", Owner)
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureTestServices(services => services
                .AddSingleton(_activity.Object)
                .AddSingleton(Mock.Of<IMongoDatabase>())
                .PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
                {
                    jwt.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new()
                    {
                        Issuer = Issuer
                    });
                    jwt.TokenValidationParameters.IssuerSigningKey = _signingKey;
                })));
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Publishes_the_protected_resource_metadata_to_anyone()
    {
        using var response = await GetAsync("/.well-known/oauth-protected-resource/mcp", token: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Cancellation)).Should().Be(
            """{"resource":"https://connector.test/mcp","authorization_servers":["https://issuer.test/realms/health"],"scopes_supported":["activity.read"],"bearer_methods_supported":["header"]}""");
    }

    [Fact]
    public async Task Returns_the_day_to_the_owner()
    {
        _activity.Setup(activity => activity.GetDayAsync("2026-11-01", "UTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_snapshot);

        using var response = await GetAsync(DayPath, Jwt());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Cancellation)).Should().Be(
            """{"day":"2026-11-01","timeZone":"UTC","capturedAtUtc":"2026-11-01T18:00:00Z","receivedAtUtc":"2026-11-01T18:01:00Z","totals":{"steps":1200,"activeKcal":66.5,"restingKcal":700,"exerciseMinutes":13,"standHours":3,"moveGoalKcal":500,"exerciseGoalMinutes":30,"standGoalHours":12}}""");
    }

    [Fact]
    public async Task Accepts_the_scope_in_either_claim_format()
    {
        using var response = await GetAsync(DayPath, Jwt(scopeClaim: "scp", scope: "profile activity.read"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Answers_not_found_for_a_day_without_a_snapshot()
    {
        using var response = await GetAsync(DayPath, Jwt());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/v1/activity/2026-02-30?timeZone=UTC")]
    [InlineData("/api/v1/activity/2026-11-01?timeZone=Mars/Olympus")]
    public async Task Rejects_malformed_dates_and_zones(string path)
    {
        using var response = await GetAsync(path, Jwt());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Cancellation)).Should()
            .Be("Use a YYYY-MM-DD date and IANA time zone.");
        _activity.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("GET", DayPath)]
    [InlineData("POST", "/mcp")]
    public async Task Points_callers_without_a_token_at_the_metadata(string method, string path)
    {
        using var response = await SendAsync(new(method), path, token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().Should().Be(
            """Bearer resource_metadata="https://connector.test/.well-known/oauth-protected-resource/mcp", scope="activity.read" """.TrimEnd());
    }

    [Fact]
    public async Task Turns_away_tokens_for_another_audience_or_past_their_expiry()
    {
        using var wrongAudience = await GetAsync(DayPath, Jwt(audience: "https://elsewhere.test/mcp"));
        using var expired = await GetAsync(DayPath, Jwt(expires: DateTime.UtcNow.AddMinutes(-5)));

        wrongAudience.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        expired.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forbids_other_accounts_and_tokens_without_the_read_scope()
    {
        using var stranger = await GetAsync(DayPath, Jwt(subject: "someone-else"));
        using var unscoped = await GetAsync(DayPath, Jwt(scope: "profile"));

        stranger.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        unscoped.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _activity.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    public async Task Reports_health_and_never_lets_responses_be_cached(string path)
    {
        using var response = await GetAsync(path, token: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public void Refuses_to_start_with_an_issuer_that_is_not_HTTPS()
    {
        using var misconfigured = _factory.WithWebHostBuilder(host => host.UseSetting("OAuthIssuer", "http://issuer.test"));

        var start = () => misconfigured.CreateClient();

        start.Should().Throw<OptionsValidationException>().WithMessage("*OAuthIssuer needs an HTTPS URL.*");
    }

    private static string Jwt(string subject = Owner, string scopeClaim = "scope", string scope = "activity.read",
        string audience = Resource, DateTime? expires = null)
    {
        var expiry = expires ?? DateTime.UtcNow.AddMinutes(5);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            NotBefore = expiry.AddMinutes(-10),
            Expires = expiry,
            SigningCredentials = new(_signingKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = subject, [scopeClaim] = scope }
        });
    }

    private Task<HttpResponseMessage> GetAsync(string path, string? token) => SendAsync(HttpMethod.Get, path, token);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, Cancellation);
    }
}
