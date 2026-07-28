using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

public sealed class GameLaunchService
{
    private readonly ModService _modService = new();

    public void LaunchViaSteam(Settings settings)
    {
        if (!_modService.HasConfiguredGameFolder(settings))
            throw new InvalidOperationException("Set a valid Casualties Unknown game folder first.");

        var gameRoot = _modService.GetGameRoot(settings);
        // A game root normally ends in steamapps\common\<game>. Walk upward
        // instead of assuming a fixed number of parents, so custom Steam
        // libraries still find their appmanifest file in steamapps.
        var steamApps = FindSteamAppsFolder(gameRoot);
        if (!string.IsNullOrWhiteSpace(steamApps) && Directory.Exists(steamApps))
        {
            foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                var contents = File.ReadAllText(manifest);
                var installDir = Regex.Match(contents, "\\\"installdir\\\"\\s+\\\"(?<name>[^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                if (!installDir.Success || !installDir.Groups["name"].Value.Equals(Path.GetFileName(gameRoot), StringComparison.OrdinalIgnoreCase)) continue;
                var appId = Regex.Match(contents, "\\\"appid\\\"\\s+\\\"(?<id>\\d+)\\\"");
                if (appId.Success)
                {
                    Process.Start(new ProcessStartInfo($"steam://rungameid/{appId.Groups["id"].Value}") { UseShellExecute = true });
                    return;
                }
            }
        }

        var executable = Directory.EnumerateFiles(gameRoot, "*.exe", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => Path.GetFileName(path).Contains("Casualties", StringComparison.OrdinalIgnoreCase))
            ?? Directory.EnumerateFiles(gameRoot, "*.exe", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => !Path.GetFileName(path).Contains("UnityCrashHandler", StringComparison.OrdinalIgnoreCase));
        if (executable is null) throw new FileNotFoundException("Could not find the game executable or its Steam app manifest.");
        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
    }

    private static string? FindSteamAppsFolder(string startPath)
    {
        for (var current = new DirectoryInfo(startPath); current is not null; current = current.Parent)
            if (current.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                return current.FullName;
        return null;
    }
}
