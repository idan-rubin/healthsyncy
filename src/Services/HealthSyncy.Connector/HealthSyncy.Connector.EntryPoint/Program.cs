using HealthSyncy.BL;
using HealthSyncy.Connector.EntryPoint.DI;
using HealthSyncy.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddConnector(builder.Configuration);
var issuer = builder.Configuration.Https("OAuthIssuer");
var resource = builder.Configuration.Https("McpResource");
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/readyz", async (IReadinessBL readiness, CancellationToken ct) =>
{
    await readiness.Ready(ct);
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
