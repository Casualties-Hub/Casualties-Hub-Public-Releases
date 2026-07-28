using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Parses the administrator-managed compatibility text cached from Hub Online Services.
/// Each usable line has: type | mod A | mod B-or-dash | severity | player-facing message.
/// </summary>
public static class CompatibilityFeedService
{
    public sealed record Rule(string Type, string ModA, string ModB, string Severity, string Message);

    public static IReadOnlyList<Rule> LoadCachedRules()
    {
        var text = new SettingsService().Load().CachedCompatibilityRules;
        return TryParse(text, out var rules) ? rules : [];
    }

    public static bool TryParse(string? text, out IReadOnlyList<Rule> rules)
    {
        var parsed = new List<Rule>();
        if (string.IsNullOrWhiteSpace(text)) { rules = parsed; return true; }
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = line.Split('|').Select(part => part.Trim()).ToArray();
            if (parts.Length < 5 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                DebugLogService.Info($"Ignored malformed compatibility feed line: {line}");
                continue;
            }
            var type = parts[0].ToLowerInvariant();
            if (type is not ("conflict" or "bug" or "manual"))
            {
                DebugLogService.Info($"Ignored unknown compatibility feed type: {parts[0]}");
                continue;
            }
            parsed.Add(new Rule(type, parts[1], parts[2], parts[3], string.Join(" | ", parts.Skip(4))));
        }
        rules = parsed;
        return true;
    }

    public static IReadOnlyList<string> GetConflicts(string modName, IEnumerable<string> installedNames)
    {
        var installed = installedNames.ToList();
        return LoadCachedRules().Where(rule => rule.Type == "conflict")
            .Where(rule => (DependencyCatalog.NamesMatch(rule.ModA, modName) && installed.Any(name => DependencyCatalog.NamesMatch(name, rule.ModB)))
                        || (DependencyCatalog.NamesMatch(rule.ModB, modName) && installed.Any(name => DependencyCatalog.NamesMatch(name, rule.ModA))))
            .Select(rule => DependencyCatalog.NamesMatch(rule.ModA, modName) ? rule.ModB : rule.ModA)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<string> GetKnownBugs(string modName) =>
        LoadCachedRules().Where(rule => rule.Type == "bug" && DependencyCatalog.NamesMatch(rule.ModA, modName))
            .Select(rule => rule.Message).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
