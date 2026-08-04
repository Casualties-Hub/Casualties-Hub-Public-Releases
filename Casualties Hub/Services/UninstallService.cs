using System.Diagnostics;
using System.IO;
using System.Text;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Builds the checklist of removable Hub data and hands the actual deletion to a tiny
/// temporary CMD helper, which is the only way to replace files the running
/// EXE cannot touch itself. The helper waits for this process to exit, deletes the chosen
/// paths, then deletes itself.
/// </summary>
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
                Title = "Install folder",
                Description = installDirectory,
                Paths = [installDirectory]
            },
            new UninstallItem
            {
                Key = "Settings",
                Title = "Settings",
                Description = "Your saved game folder, download inbox, and theme preferences.",
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
                Key = "NexusApiKey",
                Title = "Nexus API key",
                Description = "Your saved personal Nexus Premium API key.",
                Paths = [Path.Combine(settingsService.AppDataPath, "NexusApiKey.dat")]
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

    /// <summary>Starts the removal helper for the selected items, then returns immediately. The caller shuts the Hub down.</summary>
    public static void BeginUninstall(IEnumerable<UninstallItem> selectedItems)
    {
        var paths = selectedItems
            .SelectMany(item => item.Paths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count == 0) return;

        var stagingDirectory = Path.Combine(Path.GetTempPath(), "CasualtiesHub", "Uninstall", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        var scriptPath = Path.Combine(stagingDirectory, "Uninstall-CasualtiesHub.cmd");
        File.WriteAllText(scriptPath, BuildScript(paths, Environment.ProcessId));

        DebugLogService.Activity("Uninstall", $"Removing {paths.Count} item(s): {string.Join(", ", paths)}");
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
        {
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static string BuildScript(IReadOnlyList<string> paths, int processId)
    {
        var script = new StringBuilder();
        script.AppendLine("@echo off");
        script.AppendLine("setlocal EnableExtensions DisableDelayedExpansion");
        script.AppendLine($"set PID={processId}");
        script.AppendLine(":wait");
        script.AppendLine("tasklist /FI \"PID eq %PID%\" /NH | findstr /R /C:\"^.* %PID% .*\" >nul");
        script.AppendLine("if not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)");

        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            script.AppendLine($":retry{index}");
            script.AppendLine($"if exist \"{path}\\\" (rd /s /q \"{path}\") else if exist \"{path}\" (del /f /q \"{path}\")");
            script.AppendLine($"if not exist \"{path}\" goto done{index}");
            script.AppendLine($"set /a ATTEMPTS{index}+=1");
            script.AppendLine($"if %ATTEMPTS{index}% GEQ 5 goto done{index}");
            script.AppendLine("timeout /t 1 /nobreak >nul");
            script.AppendLine($"goto retry{index}");
            script.AppendLine($":done{index}");
        }

        script.AppendLine("endlocal");
        script.AppendLine("del \"%~f0\"");
        return script.ToString();
    }
}
