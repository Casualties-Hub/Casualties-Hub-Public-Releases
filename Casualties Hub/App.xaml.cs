using System.Windows;
using System.Windows.Threading;
using Casualties_Hub.Services;

namespace Casualties_Hub;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DebugLogService.Error("Unhandled application error", e.Exception);
        DebugLogService.CreateCrashReport(e.Exception);
        MessageBox.Show("The Hub hit an unexpected error. Open Debug Console for technical details.", "Casualties Hub", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
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
