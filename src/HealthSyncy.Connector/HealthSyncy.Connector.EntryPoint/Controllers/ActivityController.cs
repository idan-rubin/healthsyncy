using HealthSyncy.Common.Contracts;
using HealthSyncy.Connector.Domain;
using HealthSyncy.Connector.EntryPoint.DI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthSyncy.Connector.EntryPoint.Controllers;

[ApiController, Authorize(Policy = ConnectorServiceRegistration.SummaryPolicy)]
[Route("api/v1/activity")]
public sealed class ActivityController(IActivityService activity) : ControllerBase
{
    [HttpGet("{day}")]
    public async Task<IActionResult> Get(string day, [FromQuery] string timeZone, CancellationToken cancellationToken)
    {
        if (!Reporting.IsDay(day) || !Reporting.IsZone(timeZone))
            return BadRequest("Use a YYYY-MM-DD date and IANA time zone.");
        return await activity.GetDayAsync(day, timeZone, cancellationToken) is { } snapshot ? Ok(snapshot) : NotFound();
    }
}
