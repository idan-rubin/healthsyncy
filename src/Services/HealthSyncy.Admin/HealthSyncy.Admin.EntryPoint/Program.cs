using HealthSyncy.Admin.EntryPoint.DI;
using HealthSyncy.BL;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAdmin(builder.Configuration);
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
app.MapGet("/readyz", async (IReadinessBL readiness, CancellationToken ct) =>
{
    await readiness.Ready(ct);
    return Results.NoContent();
});
app.MapGet("/healthz", () => Results.NoContent());
app.MapRazorPages();
app.Run();
