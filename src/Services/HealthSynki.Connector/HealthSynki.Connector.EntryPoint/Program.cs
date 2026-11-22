using System.ComponentModel;
using HealthSynki.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddActivityStore(builder.Configuration);
var issuer = builder.Configuration.Https("OAuthIssuer");
var resource = builder.Configuration.Https("McpResource");
var owner = builder.Configuration.Required("OwnerSubject");
var metadata = new Uri(new Uri(resource), "/.well-known/oauth-protected-resource/mcp").AbsoluteUri;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.Authority = issuer;
    o.Audience = resource;
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true,
        ValidAudience = resource, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    o.Challenge = $"Bearer resource_metadata=\"{metadata}\", scope=\"activity.read\"";
    o.IncludeErrorDetails = false;
});
builder.Services.AddAuthorizationBuilder().AddPolicy("summary", p => p.RequireAuthenticatedUser()
    .RequireClaim("sub", owner).RequireAssertion(ctx => ctx.User.FindAll("scope")
    .Concat(ctx.User.FindAll("scp")).Any(c => c.Value.Split(' ').Contains("activity.read"))));
builder.Services.AddMcpServer().WithHttpTransport().WithTools<ActivityTools>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/readyz", async (ActivityStore store, CancellationToken ct) =>
{
    await store.Ping(ct);
    return Results.NoContent();
});
app.MapGet("/healthz", () => Results.NoContent());
app.MapGet("/.well-known/oauth-protected-resource/mcp", () => new
{
    resource, authorization_servers = new[] { issuer }, scopes_supported = new[] { "activity.read" },
    bearer_methods_supported = new[] { "header" }
});
app.MapControllers();
app.MapMcp("/mcp").RequireAuthorization("summary");
app.Run();

[McpServerToolType]
public sealed class ActivityTools(ActivityStore store)
{
    [McpServerTool(Name = "get_activity_day", ReadOnly = true), Description("Read one local day's activity totals and capture and receipt times in UTC. Missing metrics are unknown.")]
    public async Task<DayRow> Day(string day, string timeZone, CancellationToken ct)
    {
        Validate(day, timeZone);
        return new(day, await store.Get(day, timeZone, ct));
    }

    [McpServerTool(Name = "get_activity_week", ReadOnly = true), Description("Read the seven local calendar days ending on the requested date. Missing days have a null snapshot.")]
    public async Task<IReadOnlyList<DayRow>> Week(string endingOn, string timeZone, CancellationToken ct)
    {
        Validate(endingOn, timeZone);
        var end = DateOnly.ParseExact(endingOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (end.DayNumber < 6) throw new McpException("Date is outside the reporting range.");
        return await store.Week(end, timeZone, ct);
    }

    private static void Validate(string day, string zone)
    {
        if (!Reporting.IsDay(day) || !Reporting.TryZone(zone, out _))
            throw new McpException("Use a YYYY-MM-DD date and IANA time zone.");
    }
}
