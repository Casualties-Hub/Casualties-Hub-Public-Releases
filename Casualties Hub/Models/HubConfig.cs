namespace Casualties_Hub.Models;

public sealed class HubConfig
{
    public HubAnnouncement? CurrentAnnouncement { get; set; }

    public List<HubAnnouncement> PreviousAnnouncements { get; set; } = [];

    public HubLinks Links { get; set; } = new();
}

public sealed class HubAnnouncement
{
    public string Id { get; set; } = "";
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Message { get; set; } = "";
}

public sealed class HubLinks
{
    public string DiscordUrl { get; set; } = "";
    public string ReportUrl { get; set; } = "";
}

public enum HubConfigChannel
{
    Stable,
    Prerelease
}

public sealed record HubConfigResult(HubConfig Config, bool IsOnline, bool IsCached, bool ConfigChanged, DateTimeOffset? NextCheckUtc);
