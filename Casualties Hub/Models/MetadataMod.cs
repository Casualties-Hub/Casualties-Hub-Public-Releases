using System.Text.Json;
using System.Text.Json.Serialization;
using Casualties_Hub.Services;

namespace Casualties_Hub.Models;

/// <summary>One mod entry from the live community metadata list.</summary>
public sealed class MetadataMod
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "Unknown mod";
    public string Version { get; init; } = "Unknown";
    public string? BepinexVersion { get; init; }
    public string? DllVersion { get; init; }
    public IReadOnlyList<string> DllNames { get; init; } = [];
    public IReadOnlyDictionary<string, string> DllVersions { get; init; } = new Dictionary<string, string>();
    public string Author { get; init; } = "Unknown author";
    public string Description { get; init; } = "No description was supplied by the mod author.";
    public MetadataLinks Links { get; init; } = new();
    public MetadataStatistics Statistics { get; init; } = new();

    public string? ImageUrl => Links.Icon;
    public string? NexusUrl => Links.NexusMods;
    public string? NexusDownloadPageUrl => string.IsNullOrWhiteSpace(NexusUrl)
        ? null
        : NexusUrl.Contains('?', StringComparison.Ordinal) ? NexusUrl + "&tab=files" : NexusUrl + "?tab=files";
    public int TotalDownloads => Statistics.TotalDownloads;
    public int UniqueDownloads => Statistics.UniqueDownloads;
    public int Endorsements => Statistics.Endorsements;
    public bool HasPremiumDownload { get; set; }
    // Direct download is deliberately restricted to accounts with a configured Premium key.
    // Everyone else is sent to Nexus's own files/download page in their browser.
    public string DashboardActionLabel => IsLocallyDisabled
        ? "Re-enable in Local Mods"
        : HasPremiumDownload ? "Download" : "Open Download";
    public string RenderedDescription { get; set; } = "No description was supplied by the mod author.";
    public bool IsDescriptionExpanded { get; set; }
    public bool IsLocallyInstalled { get; set; }
    public bool IsLocallyDisabled { get; set; }
    public string LocalStatusLabel { get; set; } = "Not installed";
    public string DependenciesLabel { get; set; } = "No known dependencies";
    public string TotalDownloadsLabel => $"{TotalDownloads:N0} total downloads";
    public string UniqueDownloadsLabel => $"{UniqueDownloads:N0} unique downloads";
    public string EndorsementsLabel => $"{Endorsements:N0} endorsements";

    // The public metadata does not currently publish a modified timestamp. These
    // extension fields let the Hub use one immediately if the generator adds it.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }

    /// <summary>Optional BepInEx GUID published by newer community metadata entries.</summary>
    public string? PluginGuid
    {
        get
        {
            if (AdditionalProperties is null) return null;
            foreach (var key in new[] { "Guid", "GUID", "ModGuid", "PluginGuid", "BepInExGuid" })
            {
                if (AdditionalProperties.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
            }
            return null;
        }
    }

    public DateTimeOffset? LatestFileModifiedUtc
    {
        get
        {
            if (AdditionalProperties is null) return null;
            foreach (var key in new[] { "FileModifiedDate", "ModifiedDate", "LastUpdated", "UpdatedAt", "UpdatedDate", "UpdatedTimestamp" })
            {
                if (!AdditionalProperties.TryGetValue(key, out var value)) continue;
                if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed)) return parsed.ToUniversalTime();
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unixSeconds)) return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
            return null;
        }
    }

    public long? LatestFileSizeBytes
    {
        get
        {
            if (AdditionalProperties is null) return null;
            foreach (var key in new[] { "FileSize", "FileSizeBytes", "SizeInBytes", "file_size", "size_in_bytes" })
            {
                if (!AdditionalProperties.TryGetValue(key, out var value)) continue;
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var size)) return size;
                if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed)) return parsed;
            }
            return null;
        }
    }

    public string FileSizeLabel => LatestFileSizeBytes is { } bytes
        ? $"Size {FormatFileSize(bytes)}"
        : "Size not supplied";

    public bool IsAdultContent
    {
        get
        {
            if (AdultContentCatalog.IsAdult(Name)) return true;
            if (AdditionalProperties is null) return false;
            foreach (var key in new[] { "AdultContent", "IsAdult", "Adult", "adult_content", "adult" })
            {
                if (!AdditionalProperties.TryGetValue(key, out var value)) continue;
                if (value.ValueKind == JsonValueKind.True) return true;
                if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed)) return parsed;
            }
            return false;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}

public sealed class MetadataLinks
{
    public string? Icon { get; init; }
    public string? NexusMods { get; init; }
}

public sealed class MetadataStatistics
{
    public int Endorsements { get; init; }
    public int UniqueDownloads { get; init; }
    public int TotalDownloads { get; init; }
}
