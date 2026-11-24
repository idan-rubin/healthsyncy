using HealthSyncy.Core;
using Microsoft.AspNetCore.Mvc;

namespace HealthSyncy.Sync.EntryPoint.Controllers;

[ApiController]
[Route("api/v1/days")]
public sealed class DaysController(ActivityStore store) : ControllerBase
{
    [HttpPut("{day}")]
    public async Task<IActionResult> Put(string day, [FromBody] Upload body, CancellationToken ct)
    {
        if (!Reporting.IsDay(day) || !Reporting.TryZone(body.TimeZone, out var zone) ||
            body.CapturedAt == default || body.CapturedAt > DateTimeOffset.UtcNow.AddMinutes(5) ||
            string.CompareOrdinal(day, Reporting.DayIn(body.CapturedAt, zone!)) > 0 ||
            body.Totals is null || !body.Totals.IsValid())
            return BadRequest("Invalid day, zone, timestamp or totals.");
        if (!(await store.Settings(ct)).Enabled) return StatusCode(423);
        var result = await store.Save(day, body, ct);
        return result == WriteResult.Conflict ? Conflict("Conflicting snapshot at the same capture time.")
            : Ok(new { status = result.ToString() });
    }
}
