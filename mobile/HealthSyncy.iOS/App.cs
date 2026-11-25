using Foundation;
using HealthSyncy.iOS.DI;
using Microsoft.Extensions.DependencyInjection;
using UIKit;

namespace HealthSyncy.iOS;

public static class Program
{
    internal static IServiceProvider Services { get; } = new ServiceCollection().AddMobile().BuildServiceProvider();

    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    private readonly ISyncBL sync = Program.Services.GetRequiredService<ISyncBL>();
    private readonly ISummaryBL summary = Program.Services.GetRequiredService<ISummaryBL>();
    private readonly UILabel status = new() { Lines = 0 };
    private readonly UILabel background = new() { Lines = 0 };
    private readonly UILabel lastUpload = new() { Lines = 0 };
    private readonly UITextField host = new() { Placeholder = "HTTPS sync server", BorderStyle = UITextBorderStyle.RoundedRect, AutocapitalizationType = UITextAutocapitalizationType.None, AutocorrectionType = UITextAutocorrectionType.No };
    private readonly UITextField token = new() { Placeholder = "Device upload token", SecureTextEntry = true, BorderStyle = UITextBorderStyle.RoundedRect };

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        sync.Uploaded += sent => BeginInvokeOnMainThread(() => ShowUpload(sent));
        if (sync.IsPaired) _ = RegisterBackground();
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
            await sync.Pair(new Pairing(url.GetLeftPart(UriPartial.Authority), token.Text!));
            token.Text = "";
            _ = RegisterBackground();
        });
        refresh.TouchUpInside += (_, _) => Refresh();
        var share = UIButton.FromType(UIButtonType.System);
        share.SetTitle("Share today’s summary", UIControlState.Normal);
        share.TouchUpInside += async (_, _) => await Run(async () =>
        {
            var text = await summary.Today();
            BeginInvokeOnMainThread(() =>
            {
                var sheet = new UIActivityViewController([new NSString(text)], null);
                view.PresentViewController(sheet, true, null);
            });
        });
        var title = new UILabel { Text = "HealthSyncy", Font = UIFont.BoldSystemFontOfSize(32)! };
        var stack = new UIStackView(new UIView[] { title, host, token, connect, refresh, share, background, lastUpload, status })
            { Axis = UILayoutConstraintAxis.Vertical, Spacing = 12, TranslatesAutoresizingMaskIntoConstraints = false };
        view.View.AddSubview(stack);
        var safe = view.View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints([stack.LeadingAnchor.ConstraintEqualTo(safe.LeadingAnchor, 20),
            stack.TrailingAnchor.ConstraintEqualTo(safe.TrailingAnchor, -20), stack.TopAnchor.ConstraintEqualTo(safe.TopAnchor, 24)]);
        var window = new UIWindow(scene) { RootViewController = view };
        window.MakeKeyAndVisible();
        host.Text = sync.PairedOrigin;
        lastUpload.Text = NSUserDefaults.StandardUserDefaults.StringForKey("lastUpload");
        return window;
    }

    internal void Refresh()
    {
        if (!sync.IsPaired) return;
        _ = RegisterBackground();
        _ = Run(sync.Refresh);
    }

    private async Task RegisterBackground()
    {
        try
        {
            var failed = await sync.Observe(message => BeginInvokeOnMainThread(() => status.Text = message));
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

    private void ShowUpload(int sent)
    {
        status.Text = sent == 0 ? "No readable activity yet." : "";
        if (sent == 0) return;
        var message = $"Last upload: {DateTimeOffset.Now:g} ({sent} days).";
        NSUserDefaults.StandardUserDefaults.SetString(message, "lastUpload");
        lastUpload.Text = message;
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
