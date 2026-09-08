using Avalonia;

namespace Casualties_Hub;

internal static class Program
{
    // Avalonia needs this configured before any UI type is touched, so keep the body minimal.
    [STAThread]
    public static int Main(string[] args)
    {
        // Runs without a display, so it works over SSH and in a container. Checked before
        // Avalonia starts, because a headless machine cannot get as far as a window.
        if (args.Contains("--diagnostics"))
        {
            Console.WriteLine(Diagnostics.Build());
            return 0;
        }

        // Builds every page once and exits. A page that throws in its constructor otherwise only
        // fails when its nav button is clicked, which no automated launch ever does - so this is
        // what makes "all pages load" checkable without driving the UI.
        if (args.Contains("--selftest"))
            return SelfTest.Run(BuildAvaloniaApp());

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
