using HealthSyncy.Mobile.Domain;
using HealthSyncy.Mobile.Domain.Pairings;
using HealthSyncy.Mobile.Domain.Services;
using HealthSyncy.Mobile.EntryPoint.DI;
using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.Mobile.EntryPoint;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    private const string LastUploadKey = "lastUpload";

    private readonly ISyncService _sync;
    private readonly ISummaryService _summary;
    private readonly UILabel _status = new() { Lines = 0 };
    private readonly UILabel _background = new() { Lines = 0 };
    private readonly UILabel _lastUpload = new() { Lines = 0 };

    private readonly UITextField _host = new()
    {
        Placeholder = "HTTPS sync server",
        BorderStyle = UITextBorderStyle.RoundedRect,
        AutocapitalizationType = UITextAutocapitalizationType.None,
        AutocorrectionType = UITextAutocorrectionType.No
    };

    private readonly UITextField _token = new()
    {
        Placeholder = "Device upload token",
        SecureTextEntry = true,
        BorderStyle = UITextBorderStyle.RoundedRect
    };

    public AppDelegate()
    {
        var services = new ServiceCollection().AddMobile().BuildServiceProvider();
        _sync = services.GetRequiredService<ISyncService>();
        _summary = services.GetRequiredService<ISummaryService>();
    }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        _sync.Uploaded += (_, uploaded) => BeginInvokeOnMainThread(() => ShowUpload(uploaded.Days));
        if (_sync.IsPaired)
            _ = RegisterBackgroundAsync();
        return true;
    }

    internal UIWindow CreateWindow(UIWindowScene scene)
    {
        var view = new UIViewController();
        view.View!.BackgroundColor = UIColor.SystemBackground;
        var title = new UILabel { Text = "HealthSyncy", Font = UIFont.BoldSystemFontOfSize(32)! };
        var connect = Button("Pair and sync", () => _ = RunAsync(PairAsync));
        var refresh = Button("Sync now", Refresh);
        var share = Button("Share today’s summary", () => _ = RunAsync(() => ShareAsync(view)));
        var stack = new UIStackView([title, _host, _token, connect, refresh, share, _background, _lastUpload, _status])
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 12,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        view.View.AddSubview(stack);
        var safe = view.View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints(
        [
            stack.LeadingAnchor.ConstraintEqualTo(safe.LeadingAnchor, 20),
            stack.TrailingAnchor.ConstraintEqualTo(safe.TrailingAnchor, -20),
            stack.TopAnchor.ConstraintEqualTo(safe.TopAnchor, 24)
        ]);
        var window = new UIWindow(scene) { RootViewController = view };
        window.MakeKeyAndVisible();
        _host.Text = _sync.PairedOrigin;
        _lastUpload.Text = NSUserDefaults.StandardUserDefaults.StringForKey(LastUploadKey);
        return window;
    }

    internal void Refresh()
    {
        if (!_sync.IsPaired)
            return;
        _ = RegisterBackgroundAsync();
        _ = RunAsync(_sync.RefreshAsync);
    }

    private static UIButton Button(string title, Action onTap)
    {
        var button = UIButton.FromType(UIButtonType.System);
        button.SetTitle(title, UIControlState.Normal);
        button.TouchUpInside += (_, _) => onTap();
        return button;
    }

    private async Task PairAsync()
    {
        await _sync.PairAsync(Pairing.Create(_host.Text, _token.Text));
        _token.Text = "";
        _ = RegisterBackgroundAsync();
    }

    private async Task ShareAsync(UIViewController view)
    {
        var text = await _summary.GetTodayAsync();
        BeginInvokeOnMainThread(() =>
        {
            var sheet = new UIActivityViewController([new NSString(text)], null);
            view.PresentViewController(sheet, true, null);
        });
    }

    private async Task RegisterBackgroundAsync()
    {
        try
        {
            var failed = await _sync.ObserveAsync(message => BeginInvokeOnMainThread(() => _status.Text = message));
            _background.Text = failed.Count == 0
                ? "Background delivery enabled."
                : $"Background delivery unavailable for {string.Join(", ", failed)}. Tap Sync now to retry.";
        }
        catch (Exception error)
        {
            _background.Text = $"Background registration failed ({error.GetType().Name}). Tap Sync now to retry.";
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception error)
        {
            var message = error is SyncException ? error.Message : "Sync could not finish. Try again when connected.";
            BeginInvokeOnMainThread(() => _status.Text = message);
        }
    }

    private void ShowUpload(int days)
    {
        _status.Text = days == 0 ? "No readable activity yet." : "";
        if (days == 0)
            return;
        var message = $"Last upload: {DateTimeOffset.Now:g} ({days} days).";
        NSUserDefaults.StandardUserDefaults.SetString(message, LastUploadKey);
        _lastUpload.Text = message;
    }
}
