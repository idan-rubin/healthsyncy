using System.Text.Json.Serialization;
using HealthSyncy.Common.Infrastructure.DI;
using HealthSyncy.Sync.Domain.DI;
using HealthSyncy.Sync.EntryPoint.Auth;
using HealthSyncy.Sync.EntryPoint.Options;
using Microsoft.AspNetCore.Authentication;

namespace HealthSyncy.Sync.EntryPoint.DI;

internal static class SyncServiceRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSync(IConfiguration config)
        {
            _ = services.AddHealthSyncyInfrastructure(config).AddSyncDomain();
            _ = services.AddOptions<SyncOptions>().Bind(config).ValidateDataAnnotations().ValidateOnStart();
            _ = services.AddAuthentication(UploadTokenHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, UploadTokenHandler>(UploadTokenHandler.SchemeName, null);
            _ = services.AddAuthorization();
            _ = services.AddControllers().AddJsonOptions(json =>
                json.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
            return services;
        }
    }
}
