using HealthSyncy.BL;
using HealthSyncy.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthSyncy.Connector.EntryPoint.Controllers;

[ApiController, Authorize(Policy = "summary")]
[Route("api/v1/activity")]
public sealed class ActivityController(IActivityBL activity) : ControllerBase
{
    [HttpGet("{day}")]
    public async Task<IActionResult> Get(string day, [FromQuery] string timeZone, CancellationToken ct)
    {
        if (!Reporting.IsDay(day) || !Reporting.TryZone(timeZone, out _))
            return BadRequest("Use a YYYY-MM-DD date and IANA time zone.");
        var snapshot = await activity.Day(day, timeZone, ct);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }
}
