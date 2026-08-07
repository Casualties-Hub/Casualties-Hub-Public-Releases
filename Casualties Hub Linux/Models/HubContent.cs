namespace Casualties_Hub.Models;

/// <summary>
/// The GitHub-hosted feed content. Announcements only — What Changed and
/// release information come from the build's own local Release Notes file
/// instead, so they only change when a new build ships, not when the feed does.
/// </summary>
public sealed class HubContent
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public HubAnnouncement CurrentAnnouncement { get; set; } = new();
    public List<HubAnnouncement> PreviousAnnouncements { get; set; } = [];
}

public sealed class HubAnnouncement
{
    public string Id { get; set; } = "none";
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Message { get; set; } = "No announcement right now.";
}

public sealed record HubContentResult(HubContent Content, bool IsOnline, bool IsCached, bool ContentChanged, DateTimeOffset? NextCheckUtc);
