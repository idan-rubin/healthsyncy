using HealthSyncy.BL;
using HealthSyncy.Core;
using Microsoft.AspNetCore.Mvc;

namespace HealthSyncy.Sync.EntryPoint.Controllers;

[ApiController]
[Route("api/v1/days")]
public sealed class DaysController(IActivityBL activity) : ControllerBase
{
    [HttpPut("{day}")]
    public async Task<IActionResult> Put(string day, [FromBody] Upload body, CancellationToken ct)
    {
        var result = Reporting.IsDay(day) ? await activity.Accept(day, body, ct) : UploadResult.Invalid;
        return result switch
        {
            UploadResult.Invalid => BadRequest("Invalid day, zone, timestamp or totals."),
            UploadResult.Paused => StatusCode(423),
            UploadResult.Conflict => Conflict("Conflicting snapshot at the same capture time."),
            _ => Ok(new { status = result.ToString() })
        };
    }
}
