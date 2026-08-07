using System.Diagnostics;
using System.IO;
using System.Text;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Builds the checklist of removable Hub data and hands the deletion to a small temporary shell
/// script, which is the only way to remove files the running process is still holding. The script
/// waits for this process to exit, deletes the chosen paths, then deletes itself.
/// </summary>
/// <remarks>
/// The Windows Hub writes a cmd.exe batch using tasklist, rd and del. This is the POSIX
/// equivalent, and two details in it are load-bearing:
/// <list type="number">
/// <item>Every path is single-quoted with embedded quotes escaped, because these strings come
/// from a user-editable settings file and are interpolated into a script that runs rm -rf.</item>
/// <item>Paths are checked against a containment guard first. The Windows version has no such
/// guard, so a malformed ProtectedFilesPath already reaches rd /s /q there.</item>
/// </list>
/// </remarks>
public sealed class UninstallService
{
    public static IReadOnlyList<UninstallItem> GetItems(SettingsService settingsService)
    {
        var settings = settingsService.Load();
        var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return
        [
            new UninstallItem
            {
                Key = "InstallFolder",
                Title = "Application folder",
                Description = installDirectory,
                Paths = [installDirectory]
            },
            new UninstallItem
            {
                Key = "Settings",
                Title = "Settings",
                Description = "Your saved game folder, download folder, and theme preferences.",
                Paths = [Path.Combine(settingsService.AppDataPath, "Settings.json")]
            },
            new UninstallItem
            {
                Key = "ProtectedAssets",
                Title = "Protected assets",
                Description = "Plugin files backed up through the Protected Assets page.",
                Paths =
                [
                    Path.Combine(settingsService.AppDataPath, "ProtectedFiles.json"),
                    Path.Combine(settingsService.AppDataPath, settings.ProtectedFilesPath)
                ]
            },
            new UninstallItem
            {
                Key = "Backups",
                Title = "Backups",
                Description = "Copies of your plugins folder taken from the Backups page.",
                Paths = [ModService.BackupRoot(settings)]
            },
            new UninstallItem
            {
                Key = "NexusApiKey",
                Title = "Nexus API key",
                Description = "Your saved personal Nexus Premium API key.",
                // Both files: the encrypted payload and the key that decrypts it. Leaving the
                // key file behind would strand an unreadable secret in the data folder.
                Paths =
                [
                    Path.Combine(settingsService.AppDataPath, "NexusApiKey.dat"),
                    Path.Combine(settingsService.AppDataPath, "NexusApiKey.key")
                ]
            },
            new UninstallItem
            {
                Key = "LogsAndCrashReports",
                Title = "Logs and crash reports",
                Description = "Local activity logs and crash reports kept for support.",
                Paths = [DebugLogService.LogDirectory, DebugLogService.CrashReportDirectory]
            }
        ];
    }

    /// <summary>Starts the removal helper for the selected items, then returns. The caller shuts the Hub down.</summary>
    public static void BeginUninstall(IEnumerable<UninstallItem> selectedItems)
    {
        var paths = ResolveDeletablePaths(selectedItems);
        if (paths.Count == 0)
        {
            DebugLogService.Info("Uninstall was requested but nothing safe to remove was selected.");
            return;
        }

        var stagingDirectory = Path.Combine(Path.GetTempPath(), "CasualtiesHub", "Uninstall", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        var scriptPath = Path.Combine(stagingDirectory, "uninstall-casualties-hub.sh");

        File.WriteAllText(scriptPath, BuildScript(paths, Environment.ProcessId, stagingDirectory));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        DebugLogService.Activity("Uninstall", $"Removing {paths.Count} item(s): {string.Join(", ", paths)}");

        // UseShellExecute = false, deliberately. With it true the request goes through xdg-open,
        // which opens a .sh in a text editor rather than running it.
        Process.Start(new ProcessStartInfo("/bin/sh", scriptPath) { UseShellExecute = false });
    }

    /// <summary>
    /// The selected paths, de-duplicated and filtered to those the Hub is allowed to delete.
    /// </summary>
    /// <remarks>
    /// Ordinal de-duplication, not OrdinalIgnoreCase: on a case-sensitive filesystem two paths
    /// differing only in case are different directories, and collapsing them would drop one.
    /// </remarks>
    internal static List<string> ResolveDeletablePaths(IEnumerable<UninstallItem> selectedItems) =>
        selectedItems
            .SelectMany(item => item.Paths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .Where(IsSafeToDelete)
            .ToList();

    /// <summary>
    /// Rejects anything that is not clearly Hub-owned data.
    /// </summary>
    /// <remarks>
    /// ProtectedFilesPath and BackupPath come from a JSON file the user can edit by hand, and a
    /// value like "/" or ".." would otherwise be interpolated straight into rm -rf. A misconfigured
    /// settings file should lose the Hub's own data at worst, never the home directory.
    /// </remarks>
    internal static bool IsSafeToDelete(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string full;
        try { full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (full.Length == 0) return false;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar);

        // Filesystem root, or the home directory itself.
        if (full == "/" || string.Equals(full, home, StringComparison.Ordinal)) return false;

        // Needs at least two segments below the root, so "/home" and "/usr" cannot be targeted.
        if (full.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length < 2) return false;

        // Must sit inside one of the places the Hub actually owns.
        var allowedRoots = new[]
        {
            AppContext.BaseDirectory,
            LinuxPaths.AppDataRoot(),
            Path.GetTempPath(),
        };

        return allowedRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Any(root => LinuxPaths.IsInside(full, root));
    }

    /// <summary>Marks the staging path so the self-delete can sanity-check its own target.</summary>
    internal const string StagingMarker = "/CasualtiesHub/Uninstall/";

    /// <summary>
    /// The removal script: wait for the Hub to exit, delete each path with retries, then remove
    /// its own staging directory.
    /// </summary>
    /// <param name="stagingDirectory">
    /// The directory holding the script, baked in as an absolute path.
    /// </param>
    /// <remarks>
    /// The self-delete deliberately does NOT use <c>dirname "$0"</c>. That resolves to wherever
    /// the script is running from, so a copy of the script placed anywhere else would recursively
    /// delete that location instead: running a copy from /tmp issues <c>rm -rf /tmp</c>. Baking in
    /// the absolute path removes the dependency on the script's location, and the marker check
    /// means an unexpected value is skipped rather than obeyed.
    /// </remarks>
    internal static string BuildScript(IReadOnlyList<string> paths, int processId, string stagingDirectory)
    {
        var script = new StringBuilder();
        script.Append("#!/bin/sh\n");
        script.Append("# Casualties Hub uninstall helper. Generated at runtime; deletes itself when done.\n");
        script.Append("set -u\n");
        script.Append($"PID={processId}\n");

        // kill -0 tests whether the process still exists without signalling it. Bounded so a
        // wedged Hub cannot leave this script spinning forever.
        script.Append("i=0\n");
        script.Append("while kill -0 \"$PID\" 2>/dev/null && [ \"$i\" -lt 60 ]; do\n");
        script.Append("    sleep 1\n");
        script.Append("    i=$((i+1))\n");
        script.Append("done\n");

        foreach (var path in paths)
        {
            var quoted = ShellQuote(path);
            script.Append($"n=0\n");
            script.Append($"while [ -e {quoted} ] && [ \"$n\" -lt 5 ]; do\n");
            script.Append($"    rm -rf -- {quoted}\n");
            script.Append("    n=$((n+1))\n");
            script.Append("    [ -e " + quoted + " ] && sleep 1\n");
            script.Append("done\n");
        }

        // Remove the staging directory, which also removes this script. The case guard means a
        // staging path that does not look like ours is left alone rather than recursed into.
        var staging = ShellQuote(Path.GetFullPath(stagingDirectory).TrimEnd(Path.DirectorySeparatorChar));
        script.Append($"STAGING={staging}\n");
        script.Append($"case \"$STAGING\" in\n");
        script.Append($"    *{StagingMarker}*) rm -rf -- \"$STAGING\" ;;\n");
        script.Append("    *) ;;\n");
        script.Append("esac\n");
        return script.ToString();
    }

    /// <summary>
    /// Wraps a path so the shell treats it as one literal argument.
    /// </summary>
    /// <remarks>
    /// Single quotes suppress every expansion the shell performs, so spaces, $, backticks,
    /// newlines and globs are all inert. A single quote cannot appear inside single quotes, so
    /// each one is closed, escaped and reopened: the standard '\'' idiom.
    /// </remarks>
    internal static string ShellQuote(string value) =>
        "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
