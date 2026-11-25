using System.ComponentModel.DataAnnotations;
using HealthSyncy.BL;
using HealthSyncy.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HealthSyncy.Admin.Pages;

public sealed class IndexModel(ActivityBL activity, IConfiguration config) : PageModel
{
    [BindProperty] public bool Enabled { get; set; }
    [BindProperty, Range(15, 1440)] public int StaleAfterMinutes { get; set; }
    public string Zone { get; } = config["ReportingZone"] ?? "UTC";
    public IReadOnlyList<DayRow> Days { get; private set; } = [];
    public DaySnapshot? Today => Days.LastOrDefault()?.Snapshot;
    public string Status { get; private set; } = "";
    public string Display(decimal? value) => value?.ToString("N0") ?? "—";

    public async Task OnGetAsync(CancellationToken ct)
    {
        var overview = await activity.Overview(Zone, ct);
        Enabled = overview.Settings.Enabled;
        StaleAfterMinutes = overview.Settings.StaleAfterMinutes;
        Days = overview.Days;
        Status = overview.Status switch
        {
            SyncStatus.Paused => "Uploads paused",
            SyncStatus.AwaitingToday => "Awaiting today’s snapshot",
            SyncStatus.Stale => "Snapshot is stale",
            _ => "Sync current"
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) { await OnGetAsync(ct); return Page(); }
        await activity.SaveSettings(new(Enabled, StaleAfterMinutes), ct);
        return RedirectToPage();
    }
}
