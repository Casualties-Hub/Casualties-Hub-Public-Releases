using System.Diagnostics;
using CasualtiesHub.LinuxProbe;

// Casualties Hub - Linux Edition probe.
//
// Answers the questions the port cannot answer from a Windows machine:
//   1. Where does Steam live, and can we parse its library index?
//   2. What is the EXACT on-disk casing of BepInEx/plugins/CustomSprites/Head/Body?
//   3. Is xdg-open present and is a steam:// handler registered?
//   4. Where would the Hub store its data, and is that path writable?
//
// Read-only: this tool never writes outside a single temp probe directory, which it removes.

var report = new Report();

report.Section("Casualties Hub - Linux Edition probe");
report.Line($"probe version   1.0  (for Hub 0.0.8-pre.6.1)");
report.Line($"run at          {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

SystemInfo(report);
XdgPaths(report);
CaseSensitivity(report);
var libraries = SteamLibraries(report);
var installs = SteamApps(report, libraries);
GameLayout(report, installs, args);
ShellIntegration(report);

report.Section("How to send this back");
report.Line("Save to a file and attach it in Discord:");
report.Line("  ./casualties-hub-probe > probe-report.txt 2>&1");
report.Line("");
report.Line("If the Hub did not find your game, pass the game folder explicitly:");
report.Line("  ./casualties-hub-probe \"/path/to/Casualties Unknown Demo\" > probe-report.txt 2>&1");

report.Flush();
return 0;

// ---------------------------------------------------------------------------

static void SystemInfo(Report report)
{
    report.Section("System");
    report.Try(() =>
    {
        report.Pair("os", Environment.OSVersion.VersionString);
        report.Pair("runtime", Environment.Version.ToString());
        report.Pair("arch", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString());
        report.Pair("64-bit process", Environment.Is64BitProcess.ToString());
        report.Pair("user", Environment.UserName);
        report.Pair("running as root", (Environment.UserName == "root").ToString());

        // Distro name. Useful for correlating bug reports.
        const string osRelease = "/etc/os-release";
        if (File.Exists(osRelease))
        {
            var pretty = File.ReadAllLines(osRelease)
                .FirstOrDefault(line => line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal));
            if (pretty is not null) report.Pair("distro", pretty["PRETTY_NAME=".Length..].Trim('"'));
        }

        report.Pair("session type", Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "<unset>");
        report.Pair("WAYLAND_DISPLAY", Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "<unset>");
        report.Pair("DISPLAY", Environment.GetEnvironmentVariable("DISPLAY") ?? "<unset>");
        report.Pair("in WSL", (File.Exists("/proc/sys/fs/binfmt_misc/WSLInterop") || Directory.Exists("/mnt/wslg")).ToString());
    });
}

static void XdgPaths(Report report)
{
    report.Section("Where the Hub would store its data");
    report.Try(() =>
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // SettingsService uses SpecialFolderOption.None (the default). On Unix that returns an
        // EMPTY STRING when the directory does not already exist - it does not create it. The
        // resulting Path.Combine then yields the RELATIVE path "CasualtiesHub", so the Hub would
        // write settings, logs and the Nexus key into the current working directory.
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var localAppDataCreate = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);

        report.Pair("HOME", home);
        report.Pair("XDG_DATA_HOME", Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? "<unset>");
        report.Pair("XDG_CONFIG_HOME", Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? "<unset>");
        report.Pair("LocalApplicationData", localAppData.Length == 0 ? "<EMPTY>" : localAppData);
        report.Pair("  ... with Create", localAppDataCreate.Length == 0 ? "<EMPTY>" : localAppDataCreate);

        var appData = Path.Combine(localAppData, "CasualtiesHub");
        report.Pair("=> Hub data dir", appData);
        if (!Path.IsPathRooted(appData))
        {
            report.Line("   *** BUG: that path is RELATIVE, not absolute. ***");
            report.Line("   The Hub would write its settings, logs, crash reports and Nexus API");
            report.Line($"   key into the working directory instead of {localAppDataCreate}.");
            report.Line("   Cause: ~/.local/share does not exist yet on this system.");
        }
        report.Pair("   exists", Directory.Exists(appData).ToString());
        report.Pair("   parent writable", (localAppData.Length > 0 && Writable(localAppData)).ToString());

        // Settings.cs defaults the download folder to $HOME/Downloads, which is wrong on
        // localised desktops. Report what the XDG spec actually says it should be.
        var userDirs = Path.Combine(home, ".config", "user-dirs.dirs");
        if (File.Exists(userDirs))
        {
            var download = File.ReadAllLines(userDirs)
                .FirstOrDefault(line => line.TrimStart().StartsWith("XDG_DOWNLOAD_DIR=", StringComparison.Ordinal));
            report.Pair("XDG_DOWNLOAD_DIR", download?.Split('=', 2)[1].Trim('"') ?? "<not set in user-dirs.dirs>");
        }
        else
        {
            report.Pair("user-dirs.dirs", "<absent - Hub falls back to $HOME/Downloads>");
        }

        var downloads = Path.Combine(home, "Downloads");
        report.Pair("$HOME/Downloads exists", Directory.Exists(downloads).ToString());

        // ModService writes backups beside the executable, which fails on read-only installs.
        report.Pair("app dir", AppContext.BaseDirectory);
        report.Pair("app dir writable", Writable(AppContext.BaseDirectory).ToString());
    });
}

static void CaseSensitivity(Report report)
{
    report.Section("Filesystem case sensitivity");
    report.Line("The Hub builds paths like BepInEx/Plugins with fixed casing. On a");
    report.Line("case-sensitive filesystem that fails silently if the real folder differs.");
    report.Line("");

    report.Try(() =>
    {
        foreach (var target in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     Path.GetTempPath()
                 })
        {
            report.Pair(target, DescribeCaseSensitivity(target));
        }
    });
}

static string DescribeCaseSensitivity(string directory)
{
    var probe = Path.Combine(directory, $".chprobe-{Guid.NewGuid():N}");
    try
    {
        Directory.CreateDirectory(probe);
        File.WriteAllText(Path.Combine(probe, "Aa"), "probe");
        return File.Exists(Path.Combine(probe, "aA"))
            ? "CASE-INSENSITIVE (hides casing bugs)"
            : "case-sensitive";
    }
    catch (Exception ex)
    {
        return $"could not test ({ex.GetType().Name})";
    }
    finally
    {
        try { if (Directory.Exists(probe)) Directory.Delete(probe, recursive: true); } catch { /* best effort */ }
    }
}

static List<string> SteamLibraries(Report report)
{
    report.Section("Steam libraries");

    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

    // Candidate roots, widest realistic coverage. Order matters only for reporting.
    var candidates = new List<string>();
    if (!string.IsNullOrWhiteSpace(xdgData)) candidates.Add(Path.Combine(xdgData, "Steam"));
    candidates.AddRange(
    [
        Path.Combine(home, ".local", "share", "Steam"),
        Path.Combine(home, ".steam", "steam"),
        Path.Combine(home, ".steam", "root"),
        Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"),
        "/usr/local/games/Steam",
        "/usr/games/Steam"
    ]);

    var roots = new List<string>();
    report.Line("Candidate Steam roots:");
    foreach (var candidate in candidates)
    {
        if (!Directory.Exists(candidate))
        {
            report.Line($"  -            {candidate}");
            continue;
        }

        // ~/.steam/steam is normally a symlink to ~/.local/share/Steam. Resolve so the
        // same library is not reported (or scanned) twice.
        var resolved = ResolvePath(candidate);
        var duplicate = roots.Any(existing => string.Equals(existing, resolved, StringComparison.Ordinal));
        report.Line(duplicate
            ? $"  FOUND (dup)  {candidate}  ->  {resolved}"
            : $"  FOUND        {candidate}{(resolved == candidate ? "" : $"  ->  {resolved}")}");
        if (!duplicate) roots.Add(resolved);
    }

    if (roots.Count == 0)
    {
        report.Line("");
        report.Line("  No Steam installation found. If Steam IS installed, its location is");
        report.Line("  one the Hub does not know about - please say where it lives.");
        return [];
    }

    // Every root is itself a library; libraryfolders.vdf lists the others.
    var libraries = new List<string>(roots);
    foreach (var root in roots)
    {
        foreach (var vdf in new[]
                 {
                     Path.Combine(root, "steamapps", "libraryfolders.vdf"),
                     Path.Combine(root, "config", "libraryfolders.vdf")
                 })
        {
            if (!File.Exists(vdf)) continue;

            report.Line("");
            report.Line($"  {vdf}");
            report.Try(() =>
            {
                var text = File.ReadAllText(vdf);
                var parsed = VdfNode.Parse(text);
                var container = parsed.Children.FirstOrDefault() ?? parsed;
                var paths = container.CollectLibraryPaths().ToList();

                if (paths.Count == 0)
                {
                    report.Line("    PARSED BUT FOUND NO PATHS - raw contents follow, this is a parser bug:");
                    foreach (var line in text.Split('\n').Take(40)) report.Line($"    | {line.TrimEnd()}");
                    return;
                }

                foreach (var path in paths)
                {
                    var exists = Directory.Exists(path);
                    report.Line($"    library: {path}   {(exists ? "[exists]" : "[MISSING]")}");
                    if (!exists) continue;
                    var resolved = ResolvePath(path);
                    if (!libraries.Any(existing => string.Equals(existing, resolved, StringComparison.Ordinal)))
                        libraries.Add(resolved);
                }
            }, indent: "    ");
        }
    }

    report.Line("");
    report.Line($"Distinct libraries to scan: {libraries.Count}");
    return libraries;
}

static List<(string AppId, string InstallDir, string Path)> SteamApps(Report report, List<string> libraries)
{
    report.Section("Installed Steam apps");
    var installs = new List<(string, string, string)>();
    if (libraries.Count == 0)
    {
        report.Line("  (skipped - no libraries)");
        return installs;
    }

    foreach (var library in libraries)
    {
        var steamapps = Path.Combine(library, "steamapps");
        if (!Directory.Exists(steamapps)) continue;

        report.Try(() =>
        {
            // Case-insensitive match: this is exactly the enumeration fix the port needs.
            var manifests = Directory.EnumerateFiles(steamapps, "appmanifest_*.acf",
                new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }).ToList();

            report.Line($"  {steamapps}  ({manifests.Count} manifest(s))");

            foreach (var manifest in manifests)
            {
                string appId = "?", installDir = "?";
                try
                {
                    var state = VdfNode.Parse(File.ReadAllText(manifest)).Children.FirstOrDefault();
                    appId = state?.ChildValue("appid") ?? "?";
                    installDir = state?.ChildValue("installdir") ?? "?";
                }
                catch (Exception ex)
                {
                    report.Line($"    [parse failed] {Path.GetFileName(manifest)}: {ex.Message}");
                    continue;
                }

                var gamePath = FindChildIgnoringCase(Path.Combine(steamapps, "common"), installDir);
                var interesting = installDir.Contains("Casualties", StringComparison.OrdinalIgnoreCase);
                var marker = interesting ? " <<< CASUALTIES UNKNOWN" : "";
                report.Line($"    appid {appId,-8} installdir \"{installDir}\"{marker}");
                if (gamePath is not null)
                {
                    report.Line($"      on disk: {gamePath}");
                    // The manifest's installdir casing and the real folder's casing can differ.
                    var actual = Path.GetFileName(gamePath);
                    if (!string.Equals(actual, installDir, StringComparison.Ordinal))
                        report.Line($"      NOTE: real folder name is \"{actual}\", manifest says \"{installDir}\"");
                    installs.Add((appId, installDir, gamePath));
                }
                else
                {
                    report.Line($"      on disk: NOT FOUND under {Path.Combine(steamapps, "common")}");
                }
            }
        }, indent: "    ");
    }

    return installs;
}

static void GameLayout(Report report, List<(string AppId, string InstallDir, string Path)> installs, string[] args)
{
    report.Section("Game folder layout  *** THE KEY QUESTION ***");
    report.Line("The Hub hardcodes BepInEx/Plugins, CustomSprites, Head and Body with this");
    report.Line("exact casing. Below is what is ACTUALLY on disk.");
    report.Line("");

    var roots = new List<string>();
    if (args.Length > 0 && Directory.Exists(args[0]))
    {
        report.Line($"Using the folder you passed on the command line: {args[0]}");
        roots.Add(args[0]);
    }
    roots.AddRange(installs
        .Where(install => install.InstallDir.Contains("Casualties", StringComparison.OrdinalIgnoreCase))
        .Select(install => install.Path));

    if (roots.Count == 0)
    {
        report.Line("  No Casualties Unknown install found.");
        report.Line("  If you have it installed, re-run and pass the folder:");
        report.Line("    ./casualties-hub-probe \"/path/to/Casualties Unknown Demo\"");
        return;
    }

    foreach (var root in roots.Distinct(StringComparer.Ordinal))
    {
        report.Line($"Game root: {root}");
        report.Try(() =>
        {
            // Walk the chain the Hub depends on, reporting real casing at each step.
            var bepInEx = FindChildIgnoringCase(root, "BepInEx");
            report.Pair("  BepInEx", Describe(root, "BepInEx", bepInEx));
            if (bepInEx is null)
            {
                report.Line("    BepInEx is not installed in this game folder.");
                report.Line("    Top-level entries:");
                foreach (var entry in SafeEntries(root).Take(30)) report.Line($"      {entry}");
                return;
            }

            var plugins = FindChildIgnoringCase(bepInEx, "Plugins");
            report.Pair("  Plugins", Describe(bepInEx, "Plugins", plugins));
            if (plugins is null)
            {
                report.Line("    Entries under BepInEx:");
                foreach (var entry in SafeEntries(bepInEx).Take(30)) report.Line($"      {entry}");
                return;
            }

            var dlls = Directory.EnumerateFiles(plugins, "*.dll",
                new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true }).ToList();
            report.Pair("  plugin .dll count", dlls.Count.ToString());
            var oddExtensions = dlls.Where(dll => !Path.GetExtension(dll).Equals(".dll", StringComparison.Ordinal)).ToList();
            if (oddExtensions.Count > 0)
            {
                report.Line($"    {oddExtensions.Count} file(s) use non-lowercase .dll - these are INVISIBLE to the current code:");
                foreach (var dll in oddExtensions.Take(10)) report.Line($"      {Path.GetFileName(dll)}");
            }

            var customSprites = FindChildIgnoringCase(plugins, "CustomSprites");
            report.Pair("  CustomSprites", Describe(plugins, "CustomSprites", customSprites));
            if (customSprites is null) return;

            foreach (var slot in SafeDirectories(customSprites).Take(12))
            {
                report.Line($"    slot \"{Path.GetFileName(slot)}\"");
                foreach (var part in new[] { "Head", "Body" })
                {
                    var partDir = FindChildIgnoringCase(slot, part);
                    report.Line($"      {part,-6} {Describe(slot, part, partDir)}");
                    if (partDir is null) continue;

                    var pngs = Directory.EnumerateFiles(partDir, "*.png",
                        new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }).ToList();
                    var odd = pngs.Where(png => !Path.GetExtension(png).Equals(".png", StringComparison.Ordinal)).ToList();
                    report.Line($"             {pngs.Count} png(s)"
                                + (odd.Count > 0 ? $", {odd.Count} with non-lowercase extension (INVISIBLE to current code)" : ""));
                    foreach (var png in odd.Take(5)) report.Line($"               {Path.GetFileName(png)}");
                }
            }
        });
        report.Line("");
    }
}

static string Describe(string parent, string expected, string? found)
{
    if (found is null) return $"NOT FOUND (looked for \"{expected}\")";
    var actual = Path.GetFileName(found);
    return string.Equals(actual, expected, StringComparison.Ordinal)
        ? $"\"{actual}\"  (matches the Hub's hardcoded casing)"
        : $"\"{actual}\"  *** CASING DIFFERS from \"{expected}\" - current code would MISS this ***";
}

static void ShellIntegration(Report report)
{
    report.Section("Shell integration");
    report.Try(() =>
    {
        foreach (var command in new[] { "xdg-open", "sh", "tar", "steam", "flatpak" })
            report.Pair(command, Which(command) ?? "NOT FOUND");

        report.Pair("steam:// handler", RunCapture("xdg-mime", "query default x-scheme-handler/steam") ?? "<none registered>");
        report.Pair("https handler", RunCapture("xdg-mime", "query default x-scheme-handler/https") ?? "<none registered>");
    });
}

// --- helpers ---------------------------------------------------------------

/// <summary>Finds a child entry by name, ignoring case. Returns the real path, preserving on-disk casing.</summary>
static string? FindChildIgnoringCase(string parent, string name)
{
    if (!Directory.Exists(parent)) return null;
    try
    {
        return Directory.EnumerateFileSystemEntries(parent)
            .FirstOrDefault(entry => Path.GetFileName(entry).Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    catch { return null; }
}

static IEnumerable<string> SafeEntries(string path)
{
    try { return Directory.EnumerateFileSystemEntries(path).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal); }
    catch { return []; }
}

static IEnumerable<string> SafeDirectories(string path)
{
    try { return Directory.EnumerateDirectories(path).Order(StringComparer.Ordinal); }
    catch { return []; }
}

static string ResolvePath(string path)
{
    try { return Directory.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path); }
    catch { return path; }
}

static bool Writable(string directory)
{
    var probe = Path.Combine(directory, $".wprobe-{Guid.NewGuid():N}");
    try
    {
        File.WriteAllText(probe, "probe");
        return true;
    }
    catch { return false; }
    finally
    {
        try { if (File.Exists(probe)) File.Delete(probe); } catch { /* best effort */ }
    }
}

static string? Which(string command)
{
    foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries))
    {
        try
        {
            var candidate = Path.Combine(directory, command);
            if (File.Exists(candidate)) return candidate;
        }
        catch { /* unreadable PATH entry */ }
    }
    return null;
}

static string? RunCapture(string fileName, string arguments)
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });
        if (process is null) return null;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(5000);
        return string.IsNullOrWhiteSpace(output) ? null : output;
    }
    catch { return null; }
}

// --- output ----------------------------------------------------------------

internal sealed class Report
{
    private readonly List<string> _lines = [];

    public void Section(string title)
    {
        _lines.Add("");
        _lines.Add(new string('=', 74));
        _lines.Add(title);
        _lines.Add(new string('=', 74));
    }

    public void Line(string text) => _lines.Add(text);

    public void Pair(string label, string value) => _lines.Add($"{label,-28} {value}");

    /// <summary>Runs a section body, turning any failure into a reported line instead of a crash.</summary>
    public void Try(Action body, string indent = "  ")
    {
        try { body(); }
        catch (Exception ex) { _lines.Add($"{indent}[FAILED] {ex.GetType().Name}: {ex.Message}"); }
    }

    public void Flush() => Console.WriteLine(string.Join(Environment.NewLine, _lines));
}
