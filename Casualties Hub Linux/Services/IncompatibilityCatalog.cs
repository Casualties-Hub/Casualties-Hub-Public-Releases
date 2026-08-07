using System.IO;
using System.Text.Json;

namespace Casualties_Hub.Services;

/// <summary>Loads the incompatibility list, preferring an editable copy beside the application.</summary>
public static class IncompatibilityCatalog
{
    private static readonly object Sync = new();
    private static DateTime _lastWriteUtc;
    private static string? _lastSource;
    private static IReadOnlyList<IncompatibilityEntry> _entries = [];

    public static IReadOnlyList<string> GetConflicts(string modName, IEnumerable<string> installedModNames)
    {
        var installed = installedModNames.ToList();
        var localConflicts = Load().Where(entry =>
                (DependencyCatalog.NamesMatch(entry.ModA, modName) && installed.Any(name => DependencyCatalog.NamesMatch(name, entry.ModB)))
                || (DependencyCatalog.NamesMatch(entry.ModB, modName) && installed.Any(name => DependencyCatalog.NamesMatch(name, entry.ModA))))
            .Select(entry => DependencyCatalog.NamesMatch(entry.ModA, modName) ? entry.ModB : entry.ModA)
            .ToList();
        return localConflicts.Concat(CompatibilityFeedService.GetConflicts(modName, installed))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<IncompatibilityEntry> Load()
    {
        var loaded = BundledData.Read(
            "Bundled/Catalogs/IncompatibilityCatalog.json",
            Path.Combine("Data", "Catalogs", "IncompatibilityCatalog.json"),
            Path.Combine("Services", "IncompatibilityCatalog.json")); // legacy builds
        if (loaded is not { } file) return _entries;
        lock (Sync)
        {
            if (file.Source == _lastSource && file.StampUtc == _lastWriteUtc) return _entries;
            try
            {
                _entries = JsonSerializer.Deserialize<IncompatibilityDocument>(file.Text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.Incompatibilities
                    ?.Where(entry => !string.IsNullOrWhiteSpace(entry.ModA) && !string.IsNullOrWhiteSpace(entry.ModB))
                    .ToList() ?? [];
                _lastWriteUtc = file.StampUtc;
                _lastSource = file.Source;
                DebugLogService.Info($"Loaded {_entries.Count} incompatibility entries.");
            }
            catch (Exception exception)
            {
                DebugLogService.Error("Could not read IncompatibilityCatalog.json. Keeping the last valid list.", exception);
            }
        }
        return _entries;
    }

    private sealed class IncompatibilityDocument
    {
        public List<IncompatibilityEntry> Incompatibilities { get; init; } = [];
    }

    private sealed class IncompatibilityEntry
    {
        public string ModA { get; init; } = "";
        public string ModB { get; init; } = "";
        public string? Note { get; init; }
    }
}
