using HealthSyncy.BL;
using HealthSyncy.BL.DI;
using HealthSyncy.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;
builder.Services.AddBL(cfg);
var issuer = cfg.Https("OAuthIssuer");
var origin = cfg.Https("AdminOrigin").TrimEnd('/');
var owner = cfg.Required("AdminSubject");
var client = cfg.Required("AdminClientId");
var secret = cfg.Required("AdminClientSecret");
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(
    cfg["KeyDirectory"] ?? "/var/lib/healthsyncy/keys")).SetApplicationName("HealthSyncy.Admin");
builder.Services.AddAuthentication(o =>
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
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(o => o.Conventions.AuthorizeFolder("/"));
var app = builder.Build();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/readyz", async (ActivityBL activity, CancellationToken ct) =>
{
    await activity.Ready(ct);
    return Results.NoContent();
});
app.MapGet("/healthz", () => Results.NoContent());
app.MapRazorPages();
app.Run();
