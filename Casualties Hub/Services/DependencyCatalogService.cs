using System.IO;
using System.Text.Json;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Loads DependencyCatalog.json, preferring a user-editable copy beside the application over
/// the copy bundled in the EXE. Each JSON entry is directional: the "mod" requires every
/// library in its "requires" list.
/// </summary>
public static class DependencyCatalog
{
    private static readonly object Sync = new();
    private static DateTime _lastWriteUtc;
    private static string? _lastSource;
    private static IReadOnlyDictionary<string, IReadOnlyList<DependencyRequirement>> _requirements =
        new Dictionary<string, IReadOnlyList<DependencyRequirement>>(StringComparer.Ordinal);

    public static IReadOnlyList<DependencyRequirement> GetRequirements(IEnumerable<string> modNames)
    {
        var requirements = Load();
        return modNames.SelectMany(name => requirements.TryGetValue(Key(name), out var values) ? values : [])
            .GroupBy(requirement => $"{Key(requirement.Name)}|{requirement.MinimumVersion}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(requirement => requirement.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool HasEntryForAny(IEnumerable<string> modNames)
    {
        var requirements = Load();
        return modNames.Any(name => requirements.ContainsKey(Key(name)));
    }

    public static bool NamesMatch(string left, string right) => Key(left) == Key(right);

    private static IReadOnlyDictionary<string, IReadOnlyList<DependencyRequirement>> Load()
    {
        var loaded = BundledData.Read(
            "Bundled/Catalogs/DependencyCatalog.json",
            Path.Combine("Data", "Catalogs", "DependencyCatalog.json"),
            Path.Combine("Services", "DependencyCatalog.json")); // legacy builds
        if (loaded is not { } file)
        {
            DebugLogService.Info("DependencyCatalog.json was not found; dependency prompts will ask players to check Nexus.");
            return _requirements;
        }

        lock (Sync)
        {
            if (file.Source == _lastSource && file.StampUtc == _lastWriteUtc) return _requirements;

            try
            {
                var document = JsonSerializer.Deserialize<DependencyCatalogDocument>(file.Text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new DependencyCatalogDocument();

                _requirements = document.Dependencies
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Mod))
                    .GroupBy(entry => Key(entry.Mod), StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<DependencyRequirement>)group.SelectMany(entry => entry.Requires ?? [])
                            .Where(requirement => !string.IsNullOrWhiteSpace(requirement.Name))
                            .ToList(),
                        StringComparer.Ordinal);
                _lastWriteUtc = file.StampUtc;
                _lastSource = file.Source;
                DebugLogService.Info($"Loaded {_requirements.Count} dependency entries from DependencyCatalog.json.");
            }
            catch (Exception exception)
            {
                DebugLogService.Error("Could not read DependencyCatalog.json. Keeping the last valid dependency list.", exception);
            }
        }

        return _requirements;
    }

    private static string Key(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private sealed class DependencyCatalogDocument
    {
        public List<DependencyCatalogEntry> Dependencies { get; init; } = [];
    }

    private sealed class DependencyCatalogEntry
    {
        public string Mod { get; init; } = "";
        public List<DependencyRequirement>? Requires { get; init; }
    }
}
