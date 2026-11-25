using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using HealthSyncy.Sync.EntryPoint.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HealthSyncy.Sync.EntryPoint.Auth;

internal sealed class UploadTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<SyncOptions> sync) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "UploadToken";

    private const string Prefix = "Bearer ";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        if (!IsPairedToken(header[Prefix.Length..]))
            return Task.FromResult(AuthenticateResult.Fail("Unknown device token."));
        var device = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "device")], SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(device, SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }

    private bool IsPairedToken(string token) => CryptographicOperations.FixedTimeEquals(
        Convert.FromHexString(sync.Value.UploadTokenSha256), SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
