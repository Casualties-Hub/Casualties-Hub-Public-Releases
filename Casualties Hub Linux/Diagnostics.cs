using System.Text;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub;

/// <summary>
/// A startup self-check that runs without a display, via <c>casualties-hub --diagnostics</c>.
///
/// Covers the parts that fail silently rather than loudly: embedded resources resolving out of
/// the single-file binary, and the data directory being absolute. It is also what a tester can
/// paste back when something misbehaves.
/// </summary>
public static class Diagnostics
{
    public static string Build()
    {
        var report = new StringBuilder();
        var version = HubVersion.Current();

        report.AppendLine($"Casualties Hub");
        report.AppendLine($"version   : {version}");
        report.AppendLine($"runtime   : .NET {Environment.Version}");
        report.AppendLine($"os        : {Environment.OSVersion.VersionString}");
        report.AppendLine($"user      : {Environment.UserName}{(Environment.UserName == "root" ? "  (running as root)" : "")}");
        report.AppendLine();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDir = Path.Combine(localAppData, "CasualtiesHub");
        report.AppendLine($"data dir  : {(dataDir.Length == 0 ? "<empty>" : dataDir)}");

        // GetFolderPath returns "" on Unix when the directory does not exist yet (the default
        // SpecialFolderOption.None does not create it), which silently degrades this to a
        // RELATIVE path. The Hub would then write settings, logs and the Nexus key into whatever
        // the working directory happens to be. Surface it loudly rather than let it happen.
        if (!Path.IsPathRooted(dataDir))
        {
            report.AppendLine("  *** NOT AN ABSOLUTE PATH ***");
            report.AppendLine($"  ~/.local/share does not exist, so settings would be written to: {Path.GetFullPath(dataDir)}");
        }

        report.AppendLine($"logs      : {DebugLogService.LogDirectory}");
        report.AppendLine($"app dir   : {AppContext.BaseDirectory}");
        report.AppendLine();

        report.AppendLine("bundled resources:");
        var allFound = true;
        foreach (var (label, resource) in new[]
                 {
                     ("dependency catalog", "Bundled/Catalogs/DependencyCatalog.json"),
                     ("incompatibilities", "Bundled/Catalogs/IncompatibilityCatalog.json"),
                     ("hub content", "Bundled/HubContent.json"),
                     ($"release notes {version}", $"Bundled/Release Notes/Version {version}.txt"),
                 })
        {
            var payload = BundledData.Read(resource);
            if (payload is null)
            {
                allFound = false;
                report.AppendLine($"  {label,-28} MISSING  ({resource})");
            }
            else
            {
                report.AppendLine($"  {label,-28} {payload.Value.Text.Length,7:N0} chars");
            }
        }

        report.AppendLine();
        AppendGameDetection(report);

        report.AppendLine();
        report.AppendLine(allFound
            ? "RESULT: engine self-check passed."
            : "RESULT: FAILED - bundled resources are missing from the binary.");

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// The part that can only be validated on a real install: Steam library discovery,
    /// libraryfolders.vdf parsing, and the on-disk casing of the BepInEx folders.
    /// </summary>
    private static void AppendGameDetection(StringBuilder report)
    {
        report.AppendLine("steam:");

        var libraries = SteamLibraryLocator.FindLibraries();
        if (libraries.Count == 0)
        {
            report.AppendLine("  no Steam libraries found");
            return;
        }
        foreach (var library in libraries) report.AppendLine($"  library: {library}");

        var install = SteamLibraryLocator.FindCasualtiesUnknown();
        if (install is null)
        {
            report.AppendLine("  Casualties Unknown: NOT FOUND in any Steam manifest");
            return;
        }

        report.AppendLine($"  Casualties Unknown: appid {install.AppId}");
        report.AppendLine($"    path: {install.Path}");
        report.AppendLine();

        // Report the REAL casing: Proton may have created "plugins" lowercase, and on a
        // case-sensitive filesystem that is what makes a hardcoded spelling find nothing.
        report.AppendLine("game layout (real on-disk casing):");
        var bepInEx = LinuxPaths.FindChild(install.Path, "BepInEx");
        report.AppendLine($"  BepInEx : {(bepInEx is null ? "not present" : "\"" + Path.GetFileName(bepInEx) + "\"")}");
        if (bepInEx is null) return;

        var plugins = LinuxPaths.FindChild(bepInEx, "Plugins");
        report.AppendLine($"  plugins : {(plugins is null ? "not present" : "\"" + Path.GetFileName(plugins) + "\"")}");
        if (plugins is null) return;

        try
        {
            var dlls = Directory.EnumerateFiles(plugins, "*.dll", LinuxPaths.CaseInsensitiveRecursive).ToList();
            var oddCase = dlls.Where(dll => !Path.GetExtension(dll).Equals(".dll", StringComparison.Ordinal)).ToList();
            report.AppendLine($"  mods    : {dlls.Count} .dll"
                              + (oddCase.Count > 0 ? $"  ({oddCase.Count} with non-lowercase extension, found only because matching is case-insensitive)" : ""));
            foreach (var dll in dlls.Take(15)) report.AppendLine($"            {Path.GetFileName(dll)}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            report.AppendLine($"  mods    : could not read ({exception.GetType().Name})");
        }
    }
}
