namespace Casualties_Hub.Models;

public sealed class HubContent
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public HubAnnouncement CurrentAnnouncement { get; set; } = new();
    public List<HubAnnouncement> PreviousAnnouncements { get; set; } = [];
    public string WhatChanged { get; set; } = "What changed notes are not available for this build.";
    public string ReleaseInformation { get; set; } = "No additional release information is available.";
}

public sealed class HubAnnouncement
{
    public string Id { get; set; } = "none";
    public DateTimeOffset PublishedAtUtc { get; set; }
    public string Message { get; set; } = "No announcement right now.";
}

public sealed record HubContentResult(HubContent Content, bool IsOnline, bool IsCached, bool ContentChanged, DateTimeOffset? NextCheckUtc);
