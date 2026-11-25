using System.Net.Http.Headers;
using System.Net.Http.Json;
using Foundation;
using HealthSyncy.Core;
using UIKit;

namespace HealthSyncy.iOS;

public static class Program
{
    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{

    private readonly HealthReader reader = new();
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
    private readonly SemaphoreSlim gate = new(1);
    private readonly UILabel status = new() { Lines = 0 };
    private readonly UILabel background = new() { Lines = 0 };
    private readonly UILabel lastUpload = new() { Lines = 0 };
    private readonly UITextField host = new() { Placeholder = "HTTPS sync server", BorderStyle = UITextBorderStyle.RoundedRect, AutocapitalizationType = UITextAutocapitalizationType.None, AutocorrectionType = UITextAutocorrectionType.No };
    private readonly UITextField token = new() { Placeholder = "Device upload token", SecureTextEntry = true, BorderStyle = UITextBorderStyle.RoundedRect };

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        if (NSUserDefaults.StandardUserDefaults.BoolForKey("paired")) _ = RegisterBackground();
        return true;
    }

    internal UIWindow CreateWindow(UIWindowScene scene)
    {
        var view = new UIViewController();
        view.View!.BackgroundColor = UIColor.SystemBackground;
        var connect = UIButton.FromType(UIButtonType.System);
        connect.SetTitle("Pair and sync", UIControlState.Normal);
        var refresh = UIButton.FromType(UIButtonType.System);
        refresh.SetTitle("Sync now", UIControlState.Normal);
        connect.TouchUpInside += async (_, _) => await Run(async () =>
        {
            if (!Uri.TryCreate(host.Text?.Trim(), UriKind.Absolute, out var url) || url.Scheme != "https" ||
                url.UserInfo.Length != 0 || url.PathAndQuery != "/" || url.Fragment.Length != 0 ||
                string.IsNullOrWhiteSpace(token.Text)) throw new InvalidOperationException("Enter an HTTPS origin and device token.");
            await reader.Authorize();
            new Pairing(url.GetLeftPart(UriPartial.Authority), token.Text!).Save();
            token.Text = "";
            NSUserDefaults.StandardUserDefaults.SetBool(true, "paired");
            _ = RegisterBackground();
            await Sync();
        });
        refresh.TouchUpInside += (_, _) => Refresh();
        var share = UIButton.FromType(UIButtonType.System);
        share.SetTitle("Share today’s summary", UIControlState.Normal);
        share.TouchUpInside += async (_, _) => await Run(async () =>
        {
            await reader.Authorize();
            var now = DateTimeOffset.Now;
            var zone = TimeZoneInfo.Local;
            var day = DateOnly.FromDateTime(now.LocalDateTime);
            var totals = await reader.Read(day, zone);
            if (!totals.IsValid()) throw new InvalidOperationException("No readable activity yet.");
            var lines = new List<string> { $"Apple Health · {day:yyyy-MM-dd} · {now:HH:mm} ({zone.Id})" };
            void Add(string label, decimal? value, string unit)
            {
                if (value is not null) lines.Add($"{label}: {value:0.##}{unit}");
            }
            Add("Steps", totals.Steps, "");
            Add("Active energy", totals.ActiveKcal, " kcal");
            Add("Resting energy", totals.RestingKcal, " kcal");
            Add("Exercise", totals.ExerciseMinutes, " min");
            Add("Stand", totals.StandHours, " h");
            var text = string.Join(Environment.NewLine, lines);
            BeginInvokeOnMainThread(() =>
            {
                var sheet = new UIActivityViewController([new NSString(text)], null);
                view.PresentViewController(sheet, true, null);
            });
        });
        var title = new UILabel { Text = "HealthSyncy", Font = UIFont.BoldSystemFontOfSize(32) };
        var stack = new UIStackView(new UIView[] { title, host, token, connect, refresh, share, background, lastUpload, status })
            { Axis = UILayoutConstraintAxis.Vertical, Spacing = 12, TranslatesAutoresizingMaskIntoConstraints = false };
        view.View.AddSubview(stack);
        var safe = view.View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints([stack.LeadingAnchor.ConstraintEqualTo(safe.LeadingAnchor, 20),
            stack.TrailingAnchor.ConstraintEqualTo(safe.TrailingAnchor, -20), stack.TopAnchor.ConstraintEqualTo(safe.TopAnchor, 24)]);
        var window = new UIWindow(scene) { RootViewController = view };
        window.MakeKeyAndVisible();
        host.Text = Pairing.Load()?.Origin;
        lastUpload.Text = NSUserDefaults.StandardUserDefaults.StringForKey("lastUpload");
        return window;
    }

    internal void Refresh()
    {
        if (!NSUserDefaults.StandardUserDefaults.BoolForKey("paired")) return;
        _ = RegisterBackground();
        _ = Run(Sync);
    }

    private async Task RegisterBackground()
    {
        try
        {
            var failed = await reader.Observe(Sync, message => BeginInvokeOnMainThread(() => status.Text = message));
            background.Text = failed.Count == 0 ? "Background delivery enabled." :
                $"Background delivery unavailable for {string.Join(", ", failed)}. Tap Sync now to retry.";
        }
        catch (Exception e)
        {
            background.Text = $"Background registration failed ({e.GetType().Name}). Tap Sync now to retry.";
        }
    }

    private async Task Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) { BeginInvokeOnMainThread(() => status.Text = e is InvalidOperationException ? e.Message : "Sync could not finish. Try again when connected."); }
    }

    private async Task Sync()
    {
        if (!await gate.WaitAsync(0)) return;
        try
        {
            var pairing = Pairing.Load() ?? throw new InvalidOperationException("Unlock your phone or pair it first.");
            var zone = TimeZoneInfo.Local;
            var now = DateTimeOffset.UtcNow;
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
            var sent = 0;
            for (var offset = 0; offset < 7; offset++)
            {
                var day = today.AddDays(-offset);
                var totals = await reader.Read(day, zone);
                if (!totals.IsValid()) continue;
                using var request = new HttpRequestMessage(HttpMethod.Put,
                    FormattableString.Invariant($"{pairing.Origin}/api/v1/days/{day:yyyy-MM-dd}"))
                    { Content = JsonContent.Create(new Upload(zone.Id, now, totals), MobileJson.Default.Upload) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pairing.Token);
                using var response = await http.SendAsync(request);
                if ((int)response.StatusCode == 423) throw new InvalidOperationException("Uploads are paused in Admin.");
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Upload failed (HTTP {(int)response.StatusCode}).");
                sent++;
            }
            BeginInvokeOnMainThread(() =>
            {
                status.Text = sent == 0 ? "No readable activity yet." : "";
                if (sent == 0) return;
                var message = $"Last upload: {DateTimeOffset.Now:g} ({sent} days).";
                NSUserDefaults.StandardUserDefaults.SetString(message, "lastUpload");
                lastUpload.Text = message;
            });
        }
        finally { gate.Release(); }
    }
}


[Preserve(AllMembers = true)]
[Register("SceneDelegate")]
public sealed class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    [Export("window")]
    public UIWindow? Window { get; set; }

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions options)
    {
        if (scene is UIWindowScene windowScene && UIApplication.SharedApplication.Delegate is AppDelegate app)
            Window = app.CreateWindow(windowScene);
    }

    [Export("sceneDidBecomeActive:")]
    public void DidBecomeActive(UIScene scene)
    {
        if (UIApplication.SharedApplication.Delegate is AppDelegate app) app.Refresh();
    }
}
