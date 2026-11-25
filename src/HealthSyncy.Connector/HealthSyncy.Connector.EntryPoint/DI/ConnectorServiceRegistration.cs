using HealthSyncy.Common.Infrastructure.DI;
using HealthSyncy.Connector.Contracts;
using HealthSyncy.Connector.Domain.DI;
using HealthSyncy.Connector.EntryPoint.Options;
using HealthSyncy.Connector.EntryPoint.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HealthSyncy.Connector.EntryPoint.DI;

internal static class ConnectorServiceRegistration
{
    public const string SummaryPolicy = "summary";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddConnector(IConfiguration config)
        {
            _ = services.AddHealthSyncyInfrastructure(config).AddConnectorDomain();
            _ = services.AddOptions<ConnectorOptions>().Bind(config).ValidateDataAnnotations().ValidateOnStart();
            _ = services.AddControllers();
            _ = services.AddMcpServer().WithHttpTransport().WithTools<ActivityTools>();
            return services.AddOwnerTokens().AddSummaryPolicy();
        }

        private IServiceCollection AddOwnerTokens()
        {
            _ = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            _ = services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<ConnectorOptions>>((jwt, connector) =>
                {
                    var (issuer, resource) = (connector.Value.OAuthIssuer, connector.Value.McpResource);
                    var metadata = new Uri(new Uri(resource), ProtectedResourceMetadata.Path).AbsoluteUri;
                    jwt.Authority = issuer;
                    jwt.Audience = resource;
                    jwt.MapInboundClaims = false;
                    jwt.IncludeErrorDetails = false;
                    jwt.Challenge =
                        $"Bearer resource_metadata=\"{metadata}\", scope=\"{ProtectedResourceMetadata.Scope}\"";
                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = resource,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                });
            return services;
        }

        private IServiceCollection AddSummaryPolicy()
        {
            _ = services.AddAuthorization();
            _ = services.AddOptions<AuthorizationOptions>()
                .Configure<IOptions<ConnectorOptions>>((authorization, connector) =>
                    authorization.AddPolicy(SummaryPolicy, policy => policy
                        .RequireAuthenticatedUser()
                        .RequireClaim("sub", connector.Value.OwnerSubject)
                        .RequireAssertion(context => context.User.FindAll("scope").Concat(context.User.FindAll("scp"))
                            .Any(claim => claim.Value.Split(' ').Contains(ProtectedResourceMetadata.Scope)))));
            return services;
        }
    }
}
