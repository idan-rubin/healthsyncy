using HealthSyncy.Common.Contracts;
using HealthSyncy.Sync.Contracts;
using HealthSyncy.Sync.Domain;
using Microsoft.AspNetCore.Mvc;

namespace HealthSyncy.Sync.EntryPoint.Controllers;

[ApiController]
[Route("api/v1/days")]
public sealed class DaysController(IUploadService uploads) : ControllerBase
{
    [HttpPut("{day}")]
    public async Task<IActionResult> Put(
        string day,
        [FromBody] UploadRequest request,
        CancellationToken cancellationToken)
    {
        if (!Reporting.IsDay(day))
            return BadRequest("Use a YYYY-MM-DD day.");
        return await uploads.AcceptAsync(day, request, cancellationToken) switch
        {
            Accepted accepted => Ok(new UploadResponse(accepted.Status)),
            Rejected rejected => BadRequest(rejected.Reason),
            Paused => StatusCode(StatusCodes.Status423Locked),
            Conflicting => Conflict("Conflicting snapshot at the same capture time.")
        };
    }
}
