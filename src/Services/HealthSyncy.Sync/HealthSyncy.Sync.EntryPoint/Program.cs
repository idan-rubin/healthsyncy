using System.Security.Cryptography;
using System.Text;
using HealthSyncy.Sync.EntryPoint.DI;
using HealthSyncy.BL;
using HealthSyncy.Core;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSync(builder.Configuration);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8192);
var tokenHash = Convert.FromHexString(builder.Configuration.Required("UploadTokenSha256"));
if (tokenHash.Length != 32) throw new InvalidOperationException("Expected SHA-256 digest.");
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
            !CryptographicOperations.FixedTimeEquals(tokenHash, SHA256.HashData(Encoding.UTF8.GetBytes(header[7..]))))
        {
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }
    }
    await next();
});
app.MapGet("/readyz", async (IActivityBL activity, CancellationToken ct) =>
{
    await activity.Ready(ct);
    return Results.NoContent();
});
app.MapGet("/healthz", () => Results.NoContent());
app.MapControllers();
app.Run();
