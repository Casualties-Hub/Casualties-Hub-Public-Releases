namespace Casualties_Hub.Models;

/// <summary>
/// A v1 document from the Casualties Hub configuration repository. The schema
/// version lives in the URL rather than the document, so this reader keeps
/// working against /v1/ after a /v2/ publishes alongside it.
/// </summary>
public sealed class HubConfig
{
    public HubAnnouncement? CurrentAnnouncement { get; set; }

    /// <summary>Newest first, as published.</summary>
    public List<HubAnnouncement> PreviousAnnouncements { get; set; } = [];

    public HubLinks Links { get; set; } = new();
}

public sealed class HubAnnouncement
{
    public string Id { get; set; } = "";
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Community links served with the configuration. Both are validated as https
/// before they reach here, because the Hub hands them to the shell to open.
/// </summary>
public sealed class HubLinks
{
    public string DiscordUrl { get; set; } = "";
    public string ReportUrl { get; set; } = "";
}

/// <summary>Which published channel a build reads.</summary>
public enum HubConfigChannel
{
    Stable,
    Prerelease
}

public sealed record HubConfigResult(HubConfig Config, bool IsOnline, bool IsCached, bool ConfigChanged, DateTimeOffset? NextCheckUtc);
