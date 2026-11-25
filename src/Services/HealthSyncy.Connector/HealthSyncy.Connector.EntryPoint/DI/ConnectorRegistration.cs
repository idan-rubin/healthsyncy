using HealthSyncy.BL.DI;
using HealthSyncy.Connector.EntryPoint.Tools;
using HealthSyncy.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace HealthSyncy.Connector.EntryPoint.DI;

internal static class ConnectorRegistration
{
    public static IServiceCollection AddConnector(this IServiceCollection services, IConfiguration config)
    {
        services.AddControllers();
        services.AddBL(config);
        var issuer = config.Https("OAuthIssuer");
        var resource = config.Https("McpResource");
        var owner = config.Required("OwnerSubject");
        var metadata = new Uri(new Uri(resource), "/.well-known/oauth-protected-resource/mcp").AbsoluteUri;
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
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
        services.AddAuthorizationBuilder().AddPolicy("summary", p => p.RequireAuthenticatedUser()
            .RequireClaim("sub", owner).RequireAssertion(ctx => ctx.User.FindAll("scope")
            .Concat(ctx.User.FindAll("scp")).Any(c => c.Value.Split(' ').Contains("activity.read"))));
        services.AddMcpServer().WithHttpTransport().WithTools<ActivityTools>();
        return services;
    }
}
