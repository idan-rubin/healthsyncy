using System.Net;
using System.Security.Cryptography;
using HealthSyncy.Admin.Contracts;
using HealthSyncy.Admin.Domain;
using HealthSyncy.Common.Contracts;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;

namespace HealthSyncy.Admin.UnitTests;

public sealed class AdminPortalTests : IDisposable
{
    private const string Issuer = "https://issuer.test/realms/health";
    private const string AuthorizeEndpoint = Issuer + "/protocol/openid-connect/auth";
    private const string TokenEndpoint = Issuer + "/protocol/openid-connect/token";
    private const string Callback = "https://admin.test/signin-oidc";
    private const string ClientId = "healthsyncy-admin";
    private const string Owner = "owner-subject";
    private const string Zone = "America/New_York";
    private const string Today = "2026-11-01";

    private static readonly SymmetricSecurityKey _signingKey = new(RandomNumberGenerator.GetBytes(32));
    private static readonly Uri _root = new("/", UriKind.Relative);

    private static readonly DaySnapshot _snapshot = new(Today, Zone,
        new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 18, 1, 0, DateTimeKind.Utc),
        new(12345, 432.6m, 1650, 31, 9, 500, 30, 12));

    private readonly Mock<IAdminService> _admin = new();
    private readonly StubIssuer _issuer = new();
    private readonly DirectoryInfo _keys = Directory.CreateTempSubdirectory("healthsyncy-keys-");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AdminPortalTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host
            .UseSetting("MongoUri", "mongodb://127.0.0.1:1")
            .UseSetting("OAuthIssuer", Issuer)
            .UseSetting("AdminOrigin", "https://admin.test")
            .UseSetting("AdminSubject", Owner)
            .UseSetting("AdminClientId", ClientId)
            .UseSetting("AdminClientSecret", "client-secret")
            .UseSetting("KeyDirectory", _keys.FullName)
            .UseSetting("ReportingZone", Zone)
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureTestServices(services => services
                .AddSingleton(_admin.Object)
                .AddSingleton(Mock.Of<IMongoDatabase>())
                .PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, oidc =>
                {
                    oidc.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(Metadata());
                    oidc.Backchannel = new HttpClient(_issuer);
                })));
        _client = _factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new("https://pod.internal") });
        Shows(SyncStatus.Current);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _issuer.Dispose();
        _keys.Delete(recursive: true);
    }

    [Fact]
    public async Task Sends_visitors_to_the_issuer_with_the_public_callback_and_PKCE()
    {
        using var response = await _client.GetAsync(_root, Cancellation);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.GetLeftPart(UriPartial.Path).Should().Be(AuthorizeEndpoint);
        QueryHelpers.ParseQuery(response.Headers.Location.Query)
            .ToDictionary(field => field.Key, field => field.Value.ToString(), StringComparer.Ordinal)
            .Should().Contain("client_id", ClientId)
            .And.Contain("response_type", "code")
            .And.Contain("redirect_uri", Callback)
            .And.Contain("code_challenge_method", "S256")
            .And.ContainKey("code_challenge");
        _admin.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Signs_the_owner_in_with_a_hardened_session_cookie()
    {
        using var signedIn = await SignInAsync(Owner);

        signedIn.StatusCode.Should().Be(HttpStatusCode.Redirect);
        signedIn.Headers.Location.Should().Be(_root);
        signedIn.Headers.GetValues("Set-Cookie").Should().ContainSingle(cookie =>
            cookie.StartsWith("__Host-healthsyncy=", StringComparison.Ordinal)
            && cookie.EndsWith("; path=/; secure; samesite=lax; httponly", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Redeems_the_sign_in_code_with_the_public_callback_and_the_PKCE_verifier()
    {
        using var signedIn = await SignInAsync(Owner);

        _issuer.TokenEndpoint.Should().Be(new Uri(TokenEndpoint));
        _issuer.TokenRequest.Should().Contain("grant_type", "authorization_code")
            .And.Contain("code", "authorization-code")
            .And.Contain("client_id", ClientId)
            .And.Contain("client_secret", "client-secret")
            .And.Contain("redirect_uri", Callback)
            .And.ContainKey("code_verifier");
    }

    [Fact]
    public async Task Turns_away_an_account_that_is_not_the_administrator()
    {
        using var rejected = await SignInAsync("someone-else");
        using var denied = await _client.GetAsync(rejected.Headers.Location, Cancellation);
        using var retry = await _client.GetAsync(_root, Cancellation);

        rejected.StatusCode.Should().Be(HttpStatusCode.Redirect);
        rejected.Headers.Location.Should().Be(new Uri("/denied", UriKind.Relative));
        SessionCookies(rejected).Should().BeEmpty();
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await denied.Content.ReadAsStringAsync(Cancellation)).Should()
            .Contain("<title>Access denied · HealthSyncy</title>")
            .And.Contain("This account is not the HealthSyncy administrator.");
        retry.StatusCode.Should().Be(HttpStatusCode.Redirect);
        retry.Headers.Location!.GetLeftPart(UriPartial.Path).Should().Be(AuthorizeEndpoint);
        _admin.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Explains_a_sign_in_the_identity_provider_did_not_finish()
    {
        using var challenge = await _client.GetAsync(_root, Cancellation);

        using var failed = await SubmitAsync("/signin-oidc", ("code", "authorization-code"), ("state", "tampered"));
        using var page = await _client.GetAsync(failed.Headers.Location, Cancellation);

        failed.StatusCode.Should().Be(HttpStatusCode.Redirect);
        failed.Headers.Location.Should().Be(new Uri("/denied?failed=true", UriKind.Relative));
        SessionCookies(failed).Should().BeEmpty();
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync(Cancellation)).Should()
            .Contain("<h1>Sign-in did not finish</h1>")
            .And.Contain("""<a href="/">Try again</a>""");
    }

    [Fact]
    public async Task Shows_todays_totals_and_the_last_seven_days_newest_first()
    {
        using var signedIn = await SignInAsync(Owner);

        var page = await PageAsync();

        page.Should().Contain("America/New_York · Captured 2026-11-01 18:00:00Z · Received 2026-11-01 18:01:00Z")
            .And.Contain("<strong>12,345</strong>")
            .And.Contain("<strong>433</strong>")
            .And.Contain("<strong>1,650</strong>")
            .And.Contain("<td>—</td>");
        page.IndexOf("<td>2026-11-01</td>", StringComparison.Ordinal).Should().BePositive()
            .And.BeLessThan(page.IndexOf("<td>2026-10-26</td>", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(SyncStatus.Current, "Sync current")]
    [InlineData(SyncStatus.Paused, "Uploads paused")]
    [InlineData(SyncStatus.AwaitingToday, "Awaiting today’s snapshot")]
    [InlineData(SyncStatus.Stale, "Snapshot is stale")]
    public async Task Names_the_sync_status(SyncStatus status, string expected)
    {
        Shows(status);
        using var signedIn = await SignInAsync(Owner);

        var page = await PageAsync();

        page.Should().Contain($"""<p class="status">{expected}</p>""");
    }

    [Fact]
    public async Task Saves_the_settings_the_owner_submits()
    {
        using var signedIn = await SignInAsync(Owner);

        using var response = await SubmitAsync(("Enabled", "false"), ("StaleAfterMinutes", "90"),
            ("__RequestVerificationToken", AntiforgeryToken(await PageAsync())));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be(_root);
        _admin.Verify(admin => admin.SaveSettingsAsync(new SyncSettings(false, 90), It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData("14")]
    [InlineData("1441")]
    public async Task Explains_an_alert_window_outside_the_allowed_range(string minutes)
    {
        using var signedIn = await SignInAsync(Owner);

        using var response = await SubmitAsync(("Enabled", "true"), ("StaleAfterMinutes", minutes),
            ("__RequestVerificationToken", AntiforgeryToken(await PageAsync())));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Cancellation)).Should()
            .Contain("Choose an alert window between 15 and 1440 minutes.");
        _admin.Verify(admin => admin.SaveSettingsAsync(It.IsAny<SyncSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Rejects_a_settings_form_without_the_antiforgery_token()
    {
        using var signedIn = await SignInAsync(Owner);

        using var response = await SubmitAsync(("Enabled", "false"), ("StaleAfterMinutes", "90"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _admin.Verify(admin => admin.SaveSettingsAsync(It.IsAny<SyncSettings>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Keeps_its_session_keys_in_the_configured_directory()
    {
        using var signedIn = await SignInAsync(Owner);

        _keys.EnumerateFiles("key-*.xml").Should().ContainSingle();
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/readyz")]
    [InlineData("/admin.css")]
    public async Task Serves_health_and_styles_without_a_session_and_with_hardened_headers(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), Cancellation);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
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
    public void Refuses_to_start_with_a_reporting_zone_that_is_not_an_IANA_zone()
    {
        using var misconfigured = _factory.WithWebHostBuilder(host => host.UseSetting("ReportingZone", "America/NewYork"));

        var start = () => misconfigured.CreateClient();

        start.Should().Throw<OptionsValidationException>()
            .WithMessage("*ReportingZone needs an IANA time zone, such as America/New_York.*");
    }

    [Fact]
    public void Refuses_to_start_with_a_public_origin_that_is_not_HTTPS()
    {
        using var misconfigured = _factory.WithWebHostBuilder(host => host.UseSetting("AdminOrigin", "http://admin.test"));

        var start = () => misconfigured.CreateClient();

        start.Should().Throw<OptionsValidationException>().WithMessage("*AdminOrigin needs an HTTPS URL.*");
    }

    private static OpenIdConnectConfiguration Metadata()
    {
        var metadata = new OpenIdConnectConfiguration
        {
            Issuer = Issuer,
            AuthorizationEndpoint = AuthorizeEndpoint,
            TokenEndpoint = TokenEndpoint
        };
        metadata.SigningKeys.Add(_signingKey);
        return metadata;
    }

    private static string IdToken(string subject, string nonce) => new JsonWebTokenHandler().CreateToken(
        new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = ClientId,
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new(_signingKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["sub"] = subject, ["nonce"] = nonce }
        });

    private static IEnumerable<string> SessionCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Where(cookie => cookie.StartsWith("__Host-healthsyncy=", StringComparison.Ordinal))
            : [];

    private static string AntiforgeryToken(string page)
    {
        const string field = """name="__RequestVerificationToken" type="hidden" value=""" + "\"";
        var start = page.IndexOf(field, StringComparison.Ordinal) + field.Length;
        return page[start..page.IndexOf('"', start)];
    }

    private void Shows(SyncStatus status) => _admin
        .Setup(admin => admin.GetOverviewAsync(Zone, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new SyncOverview(new(true, 180),
            [.. Reporting.WeekEnding(new(2026, 11, 1)).SkipLast(1).Select(day => new DayRow(day, null)), new(Today, _snapshot)],
            status));

    private async Task<HttpResponseMessage> SignInAsync(string subject)
    {
        using var challenge = await _client.GetAsync(_root, Cancellation);
        var authorize = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        _issuer.IdToken = IdToken(subject, authorize["nonce"].ToString());
        return await SubmitAsync("/signin-oidc", ("code", "authorization-code"), ("state", authorize["state"].ToString()));
    }

    private async Task<string> PageAsync() => WebUtility.HtmlDecode(await _client.GetStringAsync(_root, Cancellation));

    private Task<HttpResponseMessage> SubmitAsync(params (string Name, string Value)[] fields) =>
        SubmitAsync("/", fields);

    private async Task<HttpResponseMessage> SubmitAsync(string path, params (string Name, string Value)[] fields)
    {
        using var form = new FormUrlEncodedContent(fields.Select(field => KeyValuePair.Create(field.Name, field.Value)));
        return await _client.PostAsync(new Uri(path, UriKind.Relative), form, Cancellation);
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
