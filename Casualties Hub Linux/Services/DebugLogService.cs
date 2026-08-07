using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>In-app log plus persistent session and crash reports for testers.</summary>
public static class DebugLogService
{
    private static readonly object Sync = new();
    // LinuxPaths.AppDataRoot, not GetFolderPath: see the note there about the empty-string
    // return that turns these into relative paths on a machine without ~/.local/share.
    private static readonly string LogsPath = Path.Combine(LinuxPaths.AppDataRoot(), "Logs");
    private static readonly string CrashReportsPath = Path.Combine(LinuxPaths.AppDataRoot(), "Crash Reports");
    private static readonly string SessionLogPath = Path.Combine(LogsPath, $"Log {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
    private static readonly List<TimestampedEntry> RecentEntries = [];

    public static ObservableCollection<string> Entries { get; } = [];
    public static string LogDirectory => LogsPath;
    public static string CrashReportDirectory => CrashReportsPath;
    public static string CurrentSessionLogPath => SessionLogPath;

    public static void Info(string message) => Add("INFO", message);
    /// <summary>Records a meaningful launcher action.  Use this for operations users may need to diagnose.</summary>
    public static void Activity(string area, string message) => Add("ACTIVITY", $"[{area}] {message}");
    public static void Error(string message, Exception exception) => Add("ERROR", $"{message}: {exception.Message}\n{exception}");

    public static void Clear()
    {
        RunOnUi(() => Entries.Clear());
        Add("INFO", "In-app debug view cleared.");
    }

    public static string CreateCrashReport(Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(CrashReportsPath);
            var reportPath = Path.Combine(CrashReportsPath, $"Crash Report {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
            var report = new StringBuilder();
            report.AppendLine("Casualties Hub Crash Report");
            report.AppendLine($"Created: {DateTime.Now:O}");
            report.AppendLine($"Hub version: {Assembly.GetExecutingAssembly().GetName().Version}");
            report.AppendLine($"Operating system: {Environment.OSVersion}");
            report.AppendLine($".NET: {Environment.Version}");
            report.AppendLine();
            AppendGameSnapshot(report);
            report.AppendLine();
            report.AppendLine("Launcher log entries:");
            lock (Sync)
                foreach (var entry in Entries.Reverse()) report.AppendLine(entry);
            if (exception is not null)
            {
                report.AppendLine();
                report.AppendLine("Unhandled exception:");
                report.AppendLine(exception.ToString());
            }
            File.WriteAllText(reportPath, report.ToString());
            KeepFiveNewestCrashReports();
            return reportPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Creates a support log from the Hub activity observed during the last ten minutes.</summary>
    public static string CreateDiagnosticLog()
    {
        try
        {
            Directory.CreateDirectory(LogsPath);
            var diagnosticPath = Path.Combine(LogsPath, $"Diagnostic Log {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
            var since = DateTime.Now.AddMinutes(-10);
            var report = new StringBuilder();
            report.AppendLine("Casualties Hub Diagnostic Log");
            report.AppendLine($"Created: {DateTime.Now:O}");
            report.AppendLine("Included activity: last 10 minutes of this Hub session.");
            report.AppendLine();
            AppendEnvironmentSnapshot(report);
            AppendGameSnapshot(report);
            report.AppendLine();
            report.AppendLine("Recent launcher activity:");
            lock (Sync)
                foreach (var entry in RecentEntries.Where(entry => entry.Timestamp >= since))
                    report.AppendLine(entry.Line);
            File.WriteAllText(diagnosticPath, report.ToString());
            KeepFiveNewestDiagnosticLogs();
            return diagnosticPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void Add(string level, string message)
    {
        var timestamp = DateTime.Now;
        var line = $"[{timestamp:HH:mm:ss}] {level}  {message}";
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogsPath);
                File.AppendAllText(SessionLogPath, line + Environment.NewLine);
                RecentEntries.Add(new TimestampedEntry(timestamp, line));
                RecentEntries.RemoveAll(entry => entry.Timestamp < timestamp.AddMinutes(-30));
                KeepFiveNewestSessionLogs();
            }
        }
        catch { /* Logging must never stop the application. */ }
        RunOnUi(() => Entries.Insert(0, line));
    }

    private static void AppendGameSnapshot(StringBuilder report)
    {
        report.AppendLine("Configuration and active mods:");
        try
        {
            var settings = new SettingsService().Load();
            report.AppendLine($"Configured path: {settings.GamePath}");
            var modService = new ModService();
            if (!modService.HasConfiguredPluginsFolder(settings))
            {
                report.AppendLine("Plugins folder: not configured or not found.");
                return;
            }
            var pluginsPath = modService.GetPluginsPath(settings);
            report.AppendLine($"Plugins folder: {pluginsPath}");
            foreach (var entry in Directory.EnumerateFileSystemEntries(pluginsPath).OrderBy(Path.GetFileName))
                report.AppendLine($" - {Path.GetFileName(entry)}");
        }
        catch (Exception snapshotException)
        {
            report.AppendLine($"Could not create mod snapshot: {snapshotException.Message}");
        }
    }

    private static void AppendEnvironmentSnapshot(StringBuilder report)
    {
        report.AppendLine("Launcher environment:");
        report.AppendLine($"Created: {DateTime.Now:O}");
        report.AppendLine($"Hub version: {Assembly.GetExecutingAssembly().GetName().Version}");
        report.AppendLine($"Operating system: {Environment.OSVersion}");
        report.AppendLine($".NET: {Environment.Version}");
        report.AppendLine();
    }

    private static void KeepFiveNewestSessionLogs()
    {
        try
        {
            Directory.CreateDirectory(LogsPath);
            foreach (var oldLog in Directory.EnumerateFiles(LogsPath, "Log *.log")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Skip(5))
                File.Delete(oldLog);
        }
        catch { /* Best-effort cleanup only. */ }
    }

    private static void KeepFiveNewestDiagnosticLogs()
    {
        try
        {
            Directory.CreateDirectory(LogsPath);
            foreach (var oldLog in Directory.EnumerateFiles(LogsPath, "Diagnostic Log *.log")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Skip(5))
                File.Delete(oldLog);
        }
        catch { /* Best-effort cleanup only. */ }
    }

    private static void KeepFiveNewestCrashReports()
    {
        try
        {
            Directory.CreateDirectory(CrashReportsPath);
            foreach (var oldLog in Directory.EnumerateFiles(CrashReportsPath, "Crash Report *.log")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Skip(5))
                File.Delete(oldLog);
        }
        catch { /* Best-effort cleanup only. */ }
    }

    private sealed record TimestampedEntry(DateTime Timestamp, string Line);

    /// <summary>
    /// How the log marshals onto the UI thread so <see cref="Entries"/> can be bound directly.
    /// Each app installs its own at startup (WPF: Application.Current.Dispatcher; Avalonia:
    /// Dispatcher.UIThread). An implementation must run <c>action</c> inline when already on
    /// the UI thread, and post it otherwise.
    /// </summary>
    /// <remarks>
    /// Defaults to running inline. That keeps logging safe before any UI exists — during startup,
    /// in the crash handler, and under a test host — which is exactly when the log matters most.
    /// </remarks>
    public static Action<Action> UiInvoker { get; set; } = static action => action();

    private static void RunOnUi(Action action) => UiInvoker(action);
}
