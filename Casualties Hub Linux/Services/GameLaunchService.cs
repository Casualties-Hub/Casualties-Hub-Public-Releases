using System.Diagnostics;
using System.IO;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Starts Casualties Unknown through Steam.
/// </summary>
/// <remarks>
/// Linux always goes through <c>steam://rungameid/</c>. The Windows Hub falls back to running the
/// game's .exe directly, which is meaningless here: the game runs under Proton, and starting the
/// .exe outside Steam skips the compatibility tool, the Wine prefix, and the WINEDLLOVERRIDES
/// entry BepInEx needs to load at all. Without Steam we tell the user rather than launch something
/// that would appear to work and then run unmodded.
/// </remarks>
public sealed class GameLaunchService
{
    private readonly ModService _modService = new();

    public void LaunchViaSteam(Settings settings)
    {
        if (!_modService.HasConfiguredGameFolder(settings))
            throw new InvalidOperationException("Set a valid Casualties Unknown game folder first.");

        var gameRoot = _modService.GetGameRoot(settings);
        var appId = ResolveAppId(gameRoot);

        if (appId is null)
            throw new FileNotFoundException(
                "Could not find this game's Steam app manifest, so it cannot be launched through Steam. " +
                "Start it from your Steam library instead.");

        Launch(appId);
    }

    /// <summary>Reads the app id from the appmanifest that matches this install.</summary>
    private static string? ResolveAppId(string gameRoot)
    {
        // Prefer Steam's own view, which already parses every library properly.
        var known = SteamLibraryLocator.FindInstalledGames()
            .FirstOrDefault(install => LinuxPaths.IsInside(gameRoot, install.Path)
                                    || LinuxPaths.IsInside(install.Path, gameRoot));
        if (known is not null) return known.AppId;

        // The folder may sit in a library Steam no longer lists. Walk up to steamapps and read
        // the manifests directly, matching on the real folder name.
        var steamApps = FindSteamAppsFolder(gameRoot);
        if (steamApps is null || !Directory.Exists(steamApps)) return null;

        var folderName = Path.GetFileName(gameRoot);
        foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf", LinuxPaths.CaseInsensitive))
        {
            try
            {
                var state = VdfNode.Parse(File.ReadAllText(manifest)).Children.FirstOrDefault();
                var installDir = state?.ChildValue("installdir");
                if (installDir is null || !installDir.Equals(folderName, StringComparison.OrdinalIgnoreCase)) continue;

                var appId = state?.ChildValue("appid");
                if (!string.IsNullOrWhiteSpace(appId)) return appId;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Skip an unreadable manifest and keep looking.
            }
        }

        return null;
    }

    private static void Launch(string appId)
    {
        var uri = $"steam://rungameid/{appId}";

        // UseShellExecute routes through xdg-open, which honours the steam:// handler that both
        // native and Flatpak Steam register.
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            DebugLogService.Activity("Game launch", $"Requested {uri} through the desktop handler.");
            return;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            DebugLogService.Error($"Could not open {uri} through the desktop handler", exception);
        }

        // No xdg-open, or no registered handler: a minimal window manager, or a session with no
        // desktop portal. Fall back to the Steam client directly if it is on PATH.
        try
        {
            Process.Start(new ProcessStartInfo("steam", $"-applaunch {appId}") { UseShellExecute = false });
            DebugLogService.Activity("Game launch", $"Requested steam -applaunch {appId}.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Could not reach Steam. Check that Steam is installed and that xdg-open is available, " +
                $"or start the game from your Steam library. (app id {appId})", exception);
        }
    }

    private static string? FindSteamAppsFolder(string startPath)
    {
        for (var current = new DirectoryInfo(startPath); current is not null; current = current.Parent)
            if (current.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                return current.FullName;
        return null;
    }
}
