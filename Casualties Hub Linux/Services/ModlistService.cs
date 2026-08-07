using System.IO;
using System.Text;
using System.Text.Json;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>Creates self-contained, serverless modlist codes and remembers the last imported list.</summary>
public sealed class ModlistService
{
    private const string Prefix = "CUH1:";
    private readonly string _pendingPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ModlistService(SettingsService settingsService) =>
        _pendingPath = Path.Combine(settingsService.AppDataPath, "ImportedModlist.json");

    public string CreateShareCode(IEnumerable<InstalledMod> mods)
    {
        var entries = mods.Where(mod => !mod.IsDisabled && !string.IsNullOrWhiteSpace(mod.ModGuid))
            .Select(mod => new ModlistEntry
            {
                Id = mod.MetadataId ?? "",
                Name = mod.Name,
                Guid = mod.ModGuid ?? "",
                Version = mod.InstalledVersion
            })
            .DistinctBy(mod => mod.Guid, StringComparer.OrdinalIgnoreCase)
            .OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (entries.Count == 0)
            throw new InvalidDataException("No BepInEx mod GUIDs were found in the installed recognized mods.");

        DebugLogService.Activity("Share Code", $"Created a share code for {entries.Count} enabled mod(s).");
        // Compact, human-readable format: GUID~metadata-id@version. The metadata id
        // lets another player's Hub open the exact Nexus Files page even when public
        // metadata has not published the BepInEx GUID yet. Older GUID@version codes
        // remain importable.
        return Prefix + string.Join(',', entries.Select(entry =>
            Escape(entry.Guid) + "~" + Escape(entry.Id ?? "") + "@" + Escape(entry.Version ?? "")));
    }

    public IReadOnlyList<ModlistEntry> Import(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("That is not a Casualties Hub modlist code. It must begin with CUH1:.");

        var payload = code.Trim()[Prefix.Length..];
        ModlistShare? modlist;
        try
        {
            var entries = payload.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseEntry)
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Guid))
                .ToList();
            modlist = new ModlistShare { Mods = entries };
        }
        catch (Exception exception) when (exception is FormatException or JsonException or UriFormatException)
        {
            throw new InvalidDataException("The modlist code is damaged or uses an unsupported format.", exception);
        }

        if (modlist is null || modlist.FormatVersion != 1 || modlist.Mods.Count == 0)
            throw new InvalidDataException("The modlist code does not contain any mods.");

        File.WriteAllText(_pendingPath, JsonSerializer.Serialize(modlist, _jsonOptions));
        DebugLogService.Activity("Share Code", $"Imported a share code with {modlist.Mods.Count} requested mod(s).");
        return modlist.Mods;
    }

    public IReadOnlyList<ModlistEntry> LoadImported()
    {
        if (!File.Exists(_pendingPath)) return [];
        try { return JsonSerializer.Deserialize<ModlistShare>(File.ReadAllText(_pendingPath))?.Mods ?? []; }
        catch (JsonException) { return []; }
    }

    public void Ignore(ModlistEntry entry)
    {
        var current = LoadImported().Where(item => !(
            (!string.IsNullOrWhiteSpace(entry.Guid) && item.Guid.Equals(entry.Guid, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(entry.Id) && item.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase))
            || item.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))).ToList();
        File.WriteAllText(_pendingPath, JsonSerializer.Serialize(new ModlistShare { Mods = current }, _jsonOptions));
        DebugLogService.Activity("Share Code", $"Ignored requested mod {entry.Name}.");
    }

    private static ModlistEntry ParseEntry(string value)
    {
        if (!value.Contains('|'))
        {
            var compactParts = value.Split('@', 2);
            var guidAndId = compactParts.ElementAtOrDefault(0) ?? "";
            var idSeparator = guidAndId.IndexOf('~');
            var guid = Unescape(idSeparator >= 0 ? guidAndId[..idSeparator] : guidAndId);
            var id = idSeparator >= 0 ? Unescape(guidAndId[(idSeparator + 1)..]) : "";
            return new ModlistEntry
            {
                Guid = guid,
                Id = id,
                Version = Unescape(compactParts.ElementAtOrDefault(1) ?? ""),
                // GUID matching identifies installed and metadata-backed mods; this fallback
                // gives a useful label if a code references an unknown mod.
                Name = guid
            };
        }

        var parts = value.Split('|', 4);
        return new ModlistEntry
        {
            Guid = Unescape(parts.ElementAtOrDefault(0) ?? ""),
            Id = Unescape(parts.ElementAtOrDefault(1) ?? ""),
            Version = Unescape(parts.ElementAtOrDefault(2) ?? ""),
            Name = Unescape(parts.ElementAtOrDefault(3) ?? "Unknown mod")
        };
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
    private static string Unescape(string value) => Uri.UnescapeDataString(value);
}
