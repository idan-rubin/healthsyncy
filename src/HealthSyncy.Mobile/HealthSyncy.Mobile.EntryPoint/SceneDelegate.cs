namespace HealthSyncy.Mobile.EntryPoint;

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
        if (UIApplication.SharedApplication.Delegate is AppDelegate app)
            app.Refresh();
    }
}
