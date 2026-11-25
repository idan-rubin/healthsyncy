using HealthSyncy.Connector.Contracts;
using HealthSyncy.Connector.EntryPoint.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HealthSyncy.Connector.EntryPoint.Controllers;

[ApiController, AllowAnonymous]
public sealed class MetadataController(IOptions<ConnectorOptions> options) : ControllerBase
{
    [HttpGet(ProtectedResourceMetadata.Path)]
    public ProtectedResourceMetadata Get() => new(
        Resource: options.Value.McpResource,
        AuthorizationServers: [options.Value.OAuthIssuer],
        ScopesSupported: [ProtectedResourceMetadata.Scope],
        BearerMethodsSupported: ["header"]);
}
