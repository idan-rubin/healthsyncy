using System.Globalization;
using HealthSyncy.Mobile.Domain;
using HealthSyncy.Mobile.Domain.Health;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Mobile.Domain.Services;

namespace HealthSyncy.Mobile.EntryPoint.Views;

internal sealed class HomeViewController : UIViewController
{
    private const string LastSyncKey = "lastSync";
    private const string LastSyncDaysKey = "lastSyncDays";

    private static readonly NSRelativeDateTimeFormatter _ago = new()
    {
        DateTimeStyle = NSRelativeDateTimeFormatterStyle.Named,
        UnitsStyle = NSRelativeDateTimeFormatterUnitsStyle.Short
    };

    private readonly ISyncService _sync;
    private readonly ISummaryService _summary;
    private readonly UIScrollView _scroll = new()
    {
        AlwaysBounceVertical = true,
        KeyboardDismissMode = UIScrollViewKeyboardDismissMode.Interactive,
        TranslatesAutoresizingMaskIntoConstraints = false
    };
    private readonly MetricTile _steps = new(Activity.Unknown.Steps);
    private readonly MetricTile _active = new(Activity.Unknown.Active);
    private readonly MetricTile _exercise = new(Activity.Unknown.Exercise);
    private readonly MetricTile _stand = new(Activity.Unknown.Stand);
    private readonly UILabel _burnParts = Ui.Label(UIFontTextStyle.Footnote, UIColor.SecondaryLabel);
    private readonly UILabel _burn = Ui.Label(UIFontTextStyle.Title3);
    private readonly UILabel _status = Ui.Label(UIFontTextStyle.Footnote);
    private readonly UIView _dot = Ui.Dot();
    private readonly UILabel _background = Ui.Label(UIFontTextStyle.Body, text: "Background sync starting");
    private readonly UILabel _lastSync = Ui.Label(UIFontTextStyle.Body, UIColor.SecondaryLabel);
    private readonly UILabel _server = Ui.Label(UIFontTextStyle.Body);
    private readonly UITextField _host = Ui.Field("sync.example.org");
    private readonly UITextField _token = Ui.Field("Paste the device upload token");
    private readonly UIButton _pair;
    private readonly UIButton _syncNow;
    private readonly UIButton _share;
    private readonly UIButton _change;
    private readonly UIStackView _syncSection;
    private readonly UIStackView _setupSection;
    private bool _changing;

    public HomeViewController(ISyncService sync, ISummaryService summary)
    {
        _sync = sync;
        _summary = summary;
        _pair = Ui.Button("Pair and sync", true, Pair);
        _syncNow = Ui.Button("Sync now", true, () => Refresh(_syncNow));
        _share = Ui.Button("Share today’s summary", false, () => _ = RunAsync(_share, ShareAsync));
        _change = Ui.Link("Change", ToggleChange);
        _burn.Font = Ui.Font(20, UIFontWeight.Semibold, UIFontTextStyle.Title3, rounded: true);
        _server.Lines = 1;
        _server.LineBreakMode = UILineBreakMode.MiddleTruncation;
        _lastSync.TextAlignment = UITextAlignment.Right;
        _status.TextAlignment = UITextAlignment.Center;
        _host.KeyboardType = UIKeyboardType.Url;
        _host.ReturnKeyType = UIReturnKeyType.Next;
        _host.ShouldReturn = _ => _token.BecomeFirstResponder();
        _host.Text = sync.PairedOrigin;
        _token.SecureTextEntry = true;
        _token.ReturnKeyType = UIReturnKeyType.Go;
        _token.ShouldReturn = _ =>
        {
            Pair();
            return true;
        };
        _syncSection = Ui.Section("Sync", Ui.Card(
            Ui.Row(Ui.Horizontal(10, _dot, _background)),
            Ui.Row(Ui.Label(UIFontTextStyle.Body, text: "Last sync"), _lastSync),
            Ui.Row(_server, _change)));
        _setupSection = Ui.Section("Connect a server",
            Ui.Card(Ui.Row(_host), Ui.Row(_token)),
            _pair,
            Ui.Inset(Ui.Label(UIFontTextStyle.Footnote, UIColor.SecondaryLabel,
                "Optional. Pairing sends your daily totals to a server you host, so your AI assistant can read them.")));
        Say(null);
        Show(Activity.Unknown);
        Render();
    }

    public override void LoadView()
    {
        var title = Ui.Label(UIFontTextStyle.LargeTitle, text: "HealthSyncy");
        title.Font = Ui.Font(34, UIFontWeight.Bold, UIFontTextStyle.LargeTitle);
        title.AccessibilityTraits = UIAccessibilityTrait.Header;
        var icon = new UIImageView(UIImage.FromBundle("AppIcon60x60")) { ClipsToBounds = true };
        icon.Layer.CornerRadius = 8.5f;
        var top = Ui.Horizontal(16, _steps, _active);
        var bottom = Ui.Horizontal(16, _exercise, _stand);
        foreach (var pair in (UIStackView[])[top, bottom])
        {
            pair.Distribution = UIStackViewDistribution.FillEqually;
            pair.Alignment = UIStackViewAlignment.Top;
        }
        var tiles = Ui.Vertical(20, top, bottom);
        tiles.LayoutMarginsRelativeArrangement = true;
        tiles.DirectionalLayoutMargins = new(16, 0, 16, 0);
        var content = Ui.Vertical(24,
            Ui.Horizontal(10, icon, title),
            Ui.Section("Today", Ui.Card(
                tiles,
                Ui.Row(Ui.Vertical(2, Ui.Label(UIFontTextStyle.Body, text: Activity.Unknown.Burn.Label), _burnParts), _burn))),
            Ui.Vertical(12, _syncNow, _share, _status),
            _syncSection,
            _setupSection);
        content.TranslatesAutoresizingMaskIntoConstraints = false;
        _scroll.AddSubview(content);
        var root = new UIView { BackgroundColor = UIColor.SystemGroupedBackground };
        root.AddSubview(_scroll);
        NSLayoutConstraint.ActivateConstraints(
        [
            icon.WidthAnchor.ConstraintEqualTo(38),
            icon.HeightAnchor.ConstraintEqualTo(38),
            _scroll.TopAnchor.ConstraintEqualTo(root.TopAnchor),
            _scroll.BottomAnchor.ConstraintEqualTo(root.KeyboardLayoutGuide.TopAnchor),
            _scroll.LeadingAnchor.ConstraintEqualTo(root.LeadingAnchor),
            _scroll.TrailingAnchor.ConstraintEqualTo(root.TrailingAnchor),
            _scroll.ContentLayoutGuide.WidthAnchor.ConstraintEqualTo(_scroll.FrameLayoutGuide.WidthAnchor),
            content.TopAnchor.ConstraintEqualTo(_scroll.ContentLayoutGuide.TopAnchor, 12),
            content.BottomAnchor.ConstraintEqualTo(_scroll.ContentLayoutGuide.BottomAnchor, -24),
            content.LeadingAnchor.ConstraintEqualTo(root.ReadableContentGuide.LeadingAnchor),
            content.TrailingAnchor.ConstraintEqualTo(root.ReadableContentGuide.TrailingAnchor)
        ]);
        View = root;
    }

    public void Launch()
    {
        _sync.Uploaded += (_, uploaded) => BeginInvokeOnMainThread(() => ShowUpload(uploaded.Days));
        if (_sync.IsPaired)
            _ = RegisterBackgroundAsync();
    }

    public void Refresh() => Refresh(null);

    private void Refresh(UIButton? button)
    {
        Render();
        _ = RunAsync(null, LoadTodayAsync);
        if (!_sync.IsPaired)
            return;
        _ = RegisterBackgroundAsync();
        _ = RunAsync(button, _sync.RefreshAsync);
    }

    private void Render()
    {
        var paired = _sync.IsPaired;
        _syncNow.Hidden = !paired;
        _syncSection.Hidden = !paired;
        _setupSection.Hidden = paired && !_changing;
        _server.Text = _sync.PairedOrigin is { } origin ? new Uri(origin).Authority : null;
        Ui.Retitle(_change, _changing ? "Cancel" : "Change");
        ShowLastSync();
    }

    private void ToggleChange()
    {
        _changing = !_changing;
        _token.Text = "";
        Render();
        if (!_changing)
            return;
        View!.LayoutIfNeeded();
        _scroll.ScrollRectToVisible(_setupSection.ConvertRectToView(_setupSection.Bounds, _scroll), true);
    }

    private void Pair() => _ = RunAsync(_pair, PairAsync);

    private async Task PairAsync()
    {
        _ = View!.EndEditing(true);
        await _sync.PairAsync(Pairing.Create(_host.Text, _token.Text));
        _token.Text = "";
        _changing = false;
        Render();
        _ = RegisterBackgroundAsync();
    }

    private async Task ShareAsync()
    {
        var sheet = new UIActivityViewController([new NSString(await _summary.GetTodayAsync())], null);
        PresentViewController(sheet, true, null);
    }

    private async Task LoadTodayAsync() => Show(await _summary.ReadTodayAsync());

    private void Show(Activity activity)
    {
        _steps.Show(activity.Steps);
        _active.Show(activity.Active);
        _exercise.Show(activity.Exercise);
        _stand.Show(activity.Stand);
        _burn.Text = MetricTile.Amount(activity.Burn);
        _burnParts.Text = $"{MetricTile.Count(activity.Active)} active + {MetricTile.Count(activity.Resting)} resting";
    }

    private async Task RegisterBackgroundAsync()
    {
        try
        {
            var failed = await _sync.ObserveAsync(message => BeginInvokeOnMainThread(() => Say(message, true)));
            ShowBackground(failed.Count == 0
                ? null
                : $"Background sync is off for {string.Join(", ", failed)}. Tap Sync now to retry.");
        }
        catch (Exception error)
        {
            ShowBackground($"Background sync could not start ({error.GetType().Name}). Tap Sync now to retry.");
        }
    }

    private void ShowBackground(string? problem)
    {
        _background.Text = problem ?? "Background sync on";
        _dot.BackgroundColor = problem is null ? UIColor.SystemGreen : UIColor.SystemOrange;
    }

    private async Task RunAsync(UIButton? button, Func<Task> action)
    {
        if (button is not null)
        {
            Say(null);
            Ui.Busy(button, true);
        }
        try
        {
            await action();
        }
        catch (Exception error)
        {
            Say(error is SyncException ? error.Message : "Sync could not finish. Try again when connected.", true);
        }
        finally
        {
            if (button is not null)
                Ui.Busy(button, false);
        }
    }

    private void Say(string? message, bool failed = false)
    {
        _status.Text = message;
        _status.TextColor = failed ? UIColor.SystemRed : UIColor.SecondaryLabel;
        _status.Hidden = string.IsNullOrEmpty(message);
    }

    private void ShowUpload(int days)
    {
        if (days == 0)
        {
            Say("No readable activity yet.");
            return;
        }
        Say(null);
        var defaults = NSUserDefaults.StandardUserDefaults;
        defaults.SetDouble(NSDate.Now.SecondsSinceReferenceDate, LastSyncKey);
        defaults.SetInt(days, LastSyncDaysKey);
        ShowLastSync();
        if (UIApplication.SharedApplication.ApplicationState == UIApplicationState.Active)
            _ = RunAsync(null, LoadTodayAsync);
    }

    private void ShowLastSync()
    {
        var defaults = NSUserDefaults.StandardUserDefaults;
        var at = defaults.DoubleForKey(LastSyncKey);
        var days = defaults.IntForKey(LastSyncDaysKey);
        _lastSync.Text = at <= 0
            ? "Not yet"
            : string.Create(CultureInfo.CurrentCulture,
                $"{_ago.GetLocalizedString(NSDate.FromTimeIntervalSinceReferenceDate(at), NSDate.Now)} · {days} {(days == 1 ? "day" : "days")} sent");
    }
}
