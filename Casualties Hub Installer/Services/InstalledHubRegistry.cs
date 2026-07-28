using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Casualties_Hub_Installer.Services;

/// <summary>Small local registry for Hub folders installed through this wizard.</summary>
public static class InstalledHubRegistry
{
    private static readonly string RegistryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CasualtiesHub", "Installations.json");

    public static IReadOnlyList<InstalledHub> Load()
    {
        try
        {
            if (!File.Exists(RegistryPath)) return [];
            var saved = JsonSerializer.Deserialize<List<InstalledHub>>(File.ReadAllText(RegistryPath)) ?? [];
            return saved
                .Where(entry => File.Exists(Path.Combine(entry.Path, "Casualties Hub.exe")) || File.Exists(Path.Combine(entry.Path, "CasualtiesHub.exe")))
                .OrderByDescending(entry => entry.LastInstalledUtc)
                .ToList();
        }
        catch { return []; }
    }

    /// <summary>Finds registered Hub copies and common unregistered release folders without scanning whole drives.</summary>
    public static Task<IReadOnlyList<InstalledHub>> DiscoverAsync(bool scanCommonLocations, CancellationToken cancellationToken = default) =>
        Task.Run(() => Discover(scanCommonLocations, cancellationToken), cancellationToken);

    public static bool IsHubInstallation(string path) =>
        File.Exists(Path.Combine(path, "Casualties Hub.exe")) || File.Exists(Path.Combine(path, "CasualtiesHub.exe"));

    public static void Register(string path, string version)
    {
        var entries = Load().Where(entry => !entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase)).ToList();
        entries.Add(new InstalledHub(path, version, DateTimeOffset.UtcNow));
        Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath)!);
        File.WriteAllText(RegistryPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Unregister(string path)
    {
        try
        {
            var entries = ReadSavedEntries().Where(entry => !entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase)).ToList();
            if (entries.Count == 0)
            {
                if (File.Exists(RegistryPath)) File.Delete(RegistryPath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath)!);
            File.WriteAllText(RegistryPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* A stale registry record is harmless if it cannot be updated. */ }
    }

    public static string? GetInstalledVersion(string path)
    {
        var executable = new[] { "Casualties Hub.exe", "CasualtiesHub.exe" }
            .Select(file => Path.Combine(path, file))
            .FirstOrDefault(File.Exists);
        return executable is null ? null : FileVersionInfo.GetVersionInfo(executable).ProductVersion;
    }

    private static IReadOnlyList<InstalledHub> Discover(bool scanCommonLocations, CancellationToken cancellationToken)
    {
        var registered = Load().ToDictionary(entry => Path.GetFullPath(entry.Path), StringComparer.OrdinalIgnoreCase);
        var candidates = new HashSet<string>(registered.Keys, StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CasualtiesHub", "Current")
        };

        if (scanCommonLocations)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(userProfile, "Downloads")
            };
            foreach (var root in roots.Where(Directory.Exists))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var directory in FindHubDirectories(root, 3, cancellationToken)) candidates.Add(directory);
            }
        }

        return candidates
            .Where(IsHubInstallation)
            .Select(path => registered.TryGetValue(path, out var saved) ? saved : CreateDetectedEntry(path))
            .OrderByDescending(entry => entry.LastInstalledUtc)
            .ToList();
    }

    private static InstalledHub CreateDetectedEntry(string path)
    {
        var executable = new[] { "Casualties Hub.exe", "CasualtiesHub.exe" }
            .Select(file => Path.Combine(path, file)).First(File.Exists);
        var version = FileVersionInfo.GetVersionInfo(executable).ProductVersion;
        if (string.IsNullOrWhiteSpace(version) || version == "0.0.0.0") version = "Detected Hub installation";
        return new InstalledHub(path, version, File.GetLastWriteTimeUtc(executable));
    }

    private static IEnumerable<string> FindHubDirectories(string root, int maxDepth, CancellationToken cancellationToken)
    {
        var ignoredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".git", "bin", "obj", "node_modules", "packages" };
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, depth) = queue.Dequeue();
            if (IsHubInstallation(directory))
            {
                yield return directory;
                continue;
            }
            if (depth >= maxDepth) continue;
            IEnumerable<string> children;
            try { children = Directory.EnumerateDirectories(directory); }
            catch { continue; }
            foreach (var child in children)
            {
                try
                {
                    if (ignoredNames.Contains(Path.GetFileName(child)) || (File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    queue.Enqueue((child, depth + 1));
                }
                catch { /* Ignore folders that disappear or are inaccessible during discovery. */ }
            }
        }
    }

    private static List<InstalledHub> ReadSavedEntries()
    {
        if (!File.Exists(RegistryPath)) return [];
        return JsonSerializer.Deserialize<List<InstalledHub>>(File.ReadAllText(RegistryPath)) ?? [];
    }
}

public sealed record InstalledHub(string Path, string Version, DateTimeOffset LastInstalledUtc)
{
    public override string ToString() => $"{Version} — {Path}";
}
