using System.IO;

namespace Casualties_Hub.Services;

/// <summary>A game Steam has installed, resolved to its real folder on disk.</summary>
public sealed record SteamGameInstall(string AppId, string InstallDir, string Path, string LibraryPath);

/// <summary>
/// Finds Steam libraries and installed games the way Steam itself records its layout: read
/// libraryfolders.vdf for every library, then read each appmanifest_*.acf for the app id and
/// install directory.
///
/// Being manifest-driven rather than folder-name-driven matters twice over. It survives Valve
/// renaming the game's folder, and it yields the app id directly, which is what lets the game be
/// launched through steam:// instead of guessing at an executable that Proton would not run
/// natively anyway.
/// </summary>
public static class SteamLibraryLocator
{
    /// <summary>Steam roots in the order they are worth checking. Duplicates are removed after symlink resolution.</summary>
    public static IReadOnlyList<string> CandidateRoots()
    {
        if (OperatingSystem.IsWindows()) return WindowsCandidateRoots();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(xdgData)) candidates.Add(Path.Combine(xdgData, "Steam"));

        candidates.AddRange(
        [
            Path.Combine(home, ".local", "share", "Steam"),   // native, the usual one
            Path.Combine(home, ".steam", "steam"),             // legacy symlink
            Path.Combine(home, ".steam", "root"),              // legacy symlink
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"), // Flatpak
            "/usr/local/games/Steam",
            "/usr/games/Steam",
        ]);

        return candidates;
    }

    /// <summary>
    /// On Windows, Steam records where it is installed in the registry. The Program Files
    /// defaults cover an install whose registry entry is missing.
    /// </summary>
    private static IReadOnlyList<string> WindowsCandidateRoots()
    {
        var candidates = new List<string>();
        if (WindowsSteamRoot() is { } registered) candidates.Add(registered);

        foreach (var programFiles in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
        {
            var folder = Environment.GetFolderPath(programFiles);
            if (!string.IsNullOrWhiteSpace(folder)) candidates.Add(Path.Combine(folder, "Steam"));
        }

        return candidates;
    }

    /// <summary>The Steam install folder from the registry, or null when Steam has not registered one.</summary>
    public static string? WindowsSteamRoot()
    {
        if (!OperatingSystem.IsWindows()) return null;

        foreach (var (hive, subKey, valueName) in new[]
                 {
                     (Microsoft.Win32.Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                     (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                     (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
                 })
        {
            try
            {
                using var key = hive.OpenSubKey(subKey);
                if (key?.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
                    return Path.GetFullPath(value);
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or IOException or UnauthorizedAccessException)
            {
                // An unreadable hive is not an error; the Program Files defaults still apply.
            }
        }

        return null;
    }

    /// <summary>Every distinct Steam library folder on this machine.</summary>
    public static IReadOnlyList<string> FindLibraries()
    {
        var libraries = new List<string>();

        foreach (var candidate in CandidateRoots())
        {
            if (!Directory.Exists(candidate)) continue;
            var root = Resolve(candidate);
            // ~/.steam/steam is normally a symlink to ~/.local/share/Steam; without resolving
            // first, the same library would be scanned twice.
            AddDistinct(libraries, root);

            foreach (var vdf in new[]
                     {
                         Path.Combine(root, "steamapps", "libraryfolders.vdf"),
                         Path.Combine(root, "config", "libraryfolders.vdf"),
                     })
            {
                if (!File.Exists(vdf)) continue;
                foreach (var path in ReadLibraryPaths(vdf)) AddDistinct(libraries, Resolve(path));
            }
        }

        return libraries;
    }

    /// <summary>Every installed app across all libraries, read from Steam's own manifests.</summary>
    public static IReadOnlyList<SteamGameInstall> FindInstalledGames()
    {
        var installs = new List<SteamGameInstall>();

        foreach (var library in FindLibraries())
        {
            var steamapps = HubPaths.FindChild(library, "steamapps");
            if (steamapps is null) continue;

            IEnumerable<string> manifests;
            try
            {
                manifests = Directory.EnumerateFiles(steamapps, "appmanifest_*.acf", HubPaths.CaseInsensitive);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var manifest in manifests)
            {
                SteamGameInstall? install = null;
                try
                {
                    var state = VdfNode.Parse(File.ReadAllText(manifest)).Children.FirstOrDefault();
                    var appId = state?.ChildValue("appid");
                    var installDir = state?.ChildValue("installdir");
                    if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(installDir)) continue;

                    var common = HubPaths.FindChild(steamapps, "common");
                    // The manifest's installdir casing and the real folder's casing can differ.
                    var gamePath = common is null ? null : HubPaths.FindChild(common, installDir);
                    if (gamePath is null) continue;

                    install = new SteamGameInstall(appId, installDir, gamePath, library);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // A manifest we cannot read tells us nothing; keep scanning the rest.
                }

                if (install is not null) installs.Add(install);
            }
        }

        return installs;
    }

    /// <summary>The Casualties Unknown install, if Steam has one.</summary>
    public static SteamGameInstall? FindCasualtiesUnknown() =>
        FindInstalledGames().FirstOrDefault(install =>
            install.InstallDir.Contains("Casualties", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> ReadLibraryPaths(string vdfPath)
    {
        string text;
        try { text = File.ReadAllText(vdfPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { yield break; }

        VdfNode parsed;
        try { parsed = VdfNode.Parse(text); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            DebugLogService.Error($"Could not parse {vdfPath}", exception);
            yield break;
        }

        var container = parsed.Children.FirstOrDefault() ?? parsed;
        foreach (var path in container.CollectLibraryPaths())
        {
            if (Directory.Exists(path)) yield return path;
        }
    }

    private static void AddDistinct(List<string> libraries, string path)
    {
        // Ordinal on a case-sensitive filesystem, where two paths differing only by case are
        // different directories. Windows paths compare case-insensitively, so the registry root
        // and the Program Files default collapse into one entry.
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!libraries.Any(existing => string.Equals(existing, path, comparison)))
            libraries.Add(path);
    }

    private static string Resolve(string path)
    {
        try { return Directory.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return path; }
    }
}
