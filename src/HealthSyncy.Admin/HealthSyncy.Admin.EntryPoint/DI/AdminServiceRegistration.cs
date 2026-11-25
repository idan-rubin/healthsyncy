using HealthSyncy.Admin.Domain.DI;
using HealthSyncy.Admin.EntryPoint.Options;
using HealthSyncy.Common.Infrastructure.DI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;

namespace HealthSyncy.Admin.EntryPoint.DI;

internal static class AdminServiceRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAdmin(IConfiguration config)
        {
            _ = services.AddHealthSyncyInfrastructure(config).AddAdminDomain();
            _ = services.AddOptions<AdminOptions>().Bind(config).ValidateDataAnnotations().ValidateOnStart();
            _ = services.AddAuthorization();
            _ = services.AddRazorPages(pages => pages.Conventions.AuthorizeFolder("/"));
            return services.AddKeyStorage().AddOwnerSignIn();
        }

        private IServiceCollection AddKeyStorage()
        {
            _ = services.AddDataProtection().SetApplicationName("HealthSyncy.Admin");
            _ = services.AddOptions<KeyManagementOptions>().Configure<IOptions<AdminOptions>, ILoggerFactory>(
                (keys, admin, logs) => keys.XmlRepository =
                    new FileSystemXmlRepository(new DirectoryInfo(admin.Value.KeyDirectory), logs));
            return services;
        }

        private IServiceCollection AddOwnerSignIn()
        {
            _ = services
                .AddAuthentication(schemes =>
                {
                    schemes.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    schemes.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(session =>
                {
                    session.Cookie.Name = "__Host-healthsyncy";
                    session.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    session.Cookie.SameSite = SameSiteMode.Lax;
                    session.ExpireTimeSpan = TimeSpan.FromHours(4);
                })
                .AddOpenIdConnect();
            _ = services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
                .Configure<IOptions<AdminOptions>>((oidc, options) =>
                {
                    var admin = options.Value;
                    oidc.Authority = admin.OAuthIssuer;
                    oidc.ClientId = admin.AdminClientId;
                    oidc.ClientSecret = admin.AdminClientSecret;
                    oidc.MapInboundClaims = false;
                    oidc.ResponseType = "code";
                    oidc.UsePkce = true;
                    oidc.SaveTokens = false;
                    oidc.Events.OnRedirectToIdentityProvider = context =>
                    {
                        context.ProtocolMessage.RedirectUri = admin.SignInCallback;
                        return Task.CompletedTask;
                    };
                    oidc.Events.OnAuthorizationCodeReceived = context =>
                    {
                        context.TokenEndpointRequest?.RedirectUri = admin.SignInCallback;
                        return Task.CompletedTask;
                    };
                    oidc.Events.OnTokenValidated = context =>
                    {
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        if (!string.Equals(subject, admin.AdminSubject, StringComparison.Ordinal))
                            context.Fail("Account is not an administrator.");
                        return Task.CompletedTask;
                    };
                });
            return services;
        }
    }
}
