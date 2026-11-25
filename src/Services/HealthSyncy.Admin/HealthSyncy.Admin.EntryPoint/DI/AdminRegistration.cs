using HealthSyncy.BL.DI;
using HealthSyncy.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;

namespace HealthSyncy.Admin.EntryPoint.DI;

internal static class AdminRegistration
{
    public static IServiceCollection AddAdmin(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddBL(cfg);
        var issuer = cfg.Https("OAuthIssuer");
        var origin = cfg.Https("AdminOrigin").TrimEnd('/');
        var owner = cfg.Required("AdminSubject");
        var client = cfg.Required("AdminClientId");
        var secret = cfg.Required("AdminClientSecret");
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(
            cfg["KeyDirectory"] ?? "/var/lib/healthsyncy/keys")).SetApplicationName("HealthSyncy.Admin");
        services.AddAuthentication(o =>
        {
            o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        }).AddCookie(o =>
        {
            o.Cookie.Name = "__Host-healthsyncy";
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.ExpireTimeSpan = TimeSpan.FromHours(4);
        }).AddOpenIdConnect(o =>
        {
            o.Authority = issuer;
            o.ClientId = client;
            o.ClientSecret = secret;
            o.MapInboundClaims = false;
            o.ResponseType = "code";
            o.UsePkce = true;
            o.SaveTokens = false;
            o.Events.OnRedirectToIdentityProvider = ctx =>
            {
                ctx.ProtocolMessage.RedirectUri = origin + "/signin-oidc";
                return Task.CompletedTask;
            };
            o.Events.OnAuthorizationCodeReceived = ctx =>
            {
                if (ctx.TokenEndpointRequest is not null) ctx.TokenEndpointRequest.RedirectUri = origin + "/signin-oidc";
                return Task.CompletedTask;
            };
            o.Events.OnTokenValidated = ctx =>
            {
                if (ctx.Principal?.FindFirst("sub")?.Value != owner) ctx.Fail("Account is not an administrator.");
                return Task.CompletedTask;
            };
        });
        services.AddAuthorization();
        services.AddRazorPages(o => o.Conventions.AuthorizeFolder("/"));
        return services;
    }
}
