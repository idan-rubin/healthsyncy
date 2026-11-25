using HealthSyncy.Admin.EntryPoint.DI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAdmin(builder.Configuration);

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/readyz");
app.MapHealthChecks("/healthz", new() { Predicate = _ => false });
app.MapRazorPages();
app.Run();
