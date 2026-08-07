using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Casualties_Hub.Services;

namespace Casualties_Hub;

public partial class App : Application
{
    public override void Initialize()
    {
        // DebugLogService is UI-agnostic and cannot reach Avalonia's dispatcher itself, so the
        // app supplies one. Without it the bound Entries collection would be mutated from
        // background threads. Runs inline when already on the UI thread, posts otherwise.
        DebugLogService.UiInvoker = action =>
        {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        };

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // WPF wires these in the App constructor; Avalonia has no equivalent hook, so they go here.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString());
        DebugLogService.Error("Fatal unhandled application error", exception);
        DebugLogService.CreateCrashReport(exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        DebugLogService.Error("Unobserved background task error", e.Exception);
        DebugLogService.CreateCrashReport(e.Exception);
        e.SetObserved();
    }
}
