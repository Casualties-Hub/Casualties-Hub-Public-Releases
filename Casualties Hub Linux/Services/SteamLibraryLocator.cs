using System.IO;

namespace Casualties_Hub.Services;

/// <summary>A game Steam has installed, resolved to its real folder on disk.</summary>
public sealed record SteamGameInstall(string AppId, string InstallDir, string Path, string LibraryPath);

/// <summary>
/// Finds Steam libraries and installed games on Linux.
///
/// The Windows Hub scans drive letters C: through H: for "Program Files\Steam\...", which finds
/// nothing on Linux and fails silently. This replaces that with the way Steam actually records
/// its own layout: read libraryfolders.vdf for every library, then read each appmanifest_*.acf
/// for the app id and install directory.
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
            var steamapps = LinuxPaths.FindChild(library, "steamapps");
            if (steamapps is null) continue;

            IEnumerable<string> manifests;
            try
            {
                manifests = Directory.EnumerateFiles(steamapps, "appmanifest_*.acf", LinuxPaths.CaseInsensitive);
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

                    var common = LinuxPaths.FindChild(steamapps, "common");
                    // The manifest's installdir casing and the real folder's casing can differ.
                    var gamePath = common is null ? null : LinuxPaths.FindChild(common, installDir);
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
        // Ordinal: two paths differing only by case are different directories here.
        if (!libraries.Any(existing => string.Equals(existing, path, StringComparison.Ordinal)))
            libraries.Add(path);
    }

    private static string Resolve(string path)
    {
        try { return Directory.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return path; }
    }
}
