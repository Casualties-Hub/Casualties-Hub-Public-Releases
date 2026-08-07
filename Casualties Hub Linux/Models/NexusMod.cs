namespace Casualties_Hub.Models;

/// <summary>
/// The small, safe subset of Nexus metadata that the dashboard needs to show.
/// No download URL or API key is stored here.
/// </summary>
public sealed class NexusMod
{
    public int ModId { get; init; }
    public string Name { get; init; } = "Unknown mod";
    public string Author { get; init; } = "Unknown author";
    public string Version { get; init; } = "Unknown";
    public int Downloads { get; init; }
    public string? ThumbnailUrl { get; init; }

    public string PageUrl => $"https://www.nexusmods.com/scavprototype/mods/{ModId}";
    public string DownloadsLabel => $"{Downloads:N0} downloads";
}
