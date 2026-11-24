using System.ComponentModel.DataAnnotations;
using HealthSyncy.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HealthSyncy.Admin.Pages;

public sealed class IndexModel(ActivityStore store, IConfiguration config) : PageModel
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
        var settings = await store.Settings(ct);
        Enabled = settings.Enabled;
        StaleAfterMinutes = settings.StaleAfterMinutes;
        var localNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, Zone);
        Days = await store.Week(DateOnly.FromDateTime(localNow.DateTime), Zone, ct);
        Status = !Enabled ? "Uploads paused" : Today is null ? "Awaiting today’s snapshot" :
            DateTime.UtcNow - Today.CapturedAtUtc > TimeSpan.FromMinutes(StaleAfterMinutes) ?
            "Snapshot is stale" : "Sync current";
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) { await OnGetAsync(ct); return Page(); }
        await store.SetSettings(new(Enabled, StaleAfterMinutes), ct);
        return RedirectToPage();
    }
}
