using HealthSyncy.Connector.EntryPoint.DI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddConnector(builder.Configuration);

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/readyz");
app.MapHealthChecks("/healthz", new() { Predicate = _ => false });
app.MapControllers();
app.MapMcp("/mcp").RequireAuthorization(ConnectorServiceRegistration.SummaryPolicy);
app.Run();
