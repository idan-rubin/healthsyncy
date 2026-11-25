using HealthSyncy.Mobile.Domain.Services;
using HealthSyncy.Mobile.EntryPoint.DI;
using HealthSyncy.Mobile.EntryPoint.Views;
using Microsoft.Extensions.DependencyInjection;

namespace HealthSyncy.Mobile.EntryPoint;

[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    private readonly HomeViewController _home;

    public AppDelegate()
    {
        var services = new ServiceCollection().AddMobile().BuildServiceProvider();
        _home = new(services.GetRequiredService<ISyncService>(), services.GetRequiredService<ISummaryService>());
    }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        _home.Launch();
        return true;
    }

    internal UIWindow CreateWindow(UIWindowScene scene)
    {
        var window = new UIWindow(scene) { RootViewController = _home, TintColor = Ui.Accent };
        window.MakeKeyAndVisible();
        return window;
    }

    internal void Refresh() => _home.Refresh();
}
