using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HealthSyncy.Admin.Contracts;
using HealthSyncy.Admin.Domain;
using HealthSyncy.Admin.EntryPoint.Options;
using HealthSyncy.Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace HealthSyncy.Admin.EntryPoint.Pages;

public sealed class IndexModel(IAdminService admin, IOptions<AdminOptions> options) : PageModel
{
    private const string Missing = "—";

    [BindProperty]
    public bool Enabled { get; set; }

    [BindProperty, Range(15, 1440, ErrorMessage = "Choose an alert window between 15 and 1440 minutes.")]
    public int StaleAfterMinutes { get; set; }

    public string Zone { get; } = options.Value.ReportingZone;

    public string Status { get; private set; } = "";

    public IReadOnlyList<DayRow> Days { get; private set; } = [];

    public DaySnapshot? Today => Days.LastOrDefault()?.Snapshot;

    public IEnumerable<(string Label, decimal? Value, string Unit)> Metrics =>
    [
        ("Steps", Today?.Totals.Steps, "steps"),
        ("Move", Today?.Totals.ActiveKcal, "kcal"),
        ("Exercise", Today?.Totals.ExerciseMinutes, "min"),
        ("Stand", Today?.Totals.StandHours, "hr"),
        ("Resting energy", Today?.Totals.RestingKcal, "kcal")
    ];

    public static string Display(decimal? value) => value?.ToString("N0", CultureInfo.InvariantCulture) ?? Missing;

    public static string Display(DateTime? value) => value?.ToString("u", CultureInfo.InvariantCulture) ?? Missing;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var overview = await admin.GetOverviewAsync(Zone, cancellationToken);
        (Enabled, StaleAfterMinutes) = overview.Settings;
        Days = overview.Days;
        Status = overview.Status switch
        {
            SyncStatus.Paused => "Uploads paused",
            SyncStatus.AwaitingToday => "Awaiting today’s snapshot",
            SyncStatus.Stale => "Snapshot is stale",
            _ => "Sync current"
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync(cancellationToken);
            return Page();
        }
        await admin.SaveSettingsAsync(new(Enabled, StaleAfterMinutes), cancellationToken);
        return RedirectToPage();
    }
}
