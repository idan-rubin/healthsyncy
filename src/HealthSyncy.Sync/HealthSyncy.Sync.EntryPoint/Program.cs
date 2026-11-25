using HealthSyncy.Sync.EntryPoint.DI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSync(builder.Configuration);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 8192);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/readyz");
app.MapHealthChecks("/healthz", new() { Predicate = _ => false });
app.MapControllers();
app.Run();
