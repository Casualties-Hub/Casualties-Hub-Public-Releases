namespace Casualties_Hub.Models;

/// <summary>Read-only data shown by the Hub Center page.</summary>
public sealed class HubCenterState
{
    public string CurrentVersion { get; init; } = "Unknown";
    public bool OnlineServicesEnabled { get; init; }
    public bool ServiceOnline { get; init; }
    public bool ManualCheckAvailable { get; init; }
    public DateTimeOffset? NextManualCheckUtc { get; init; }
    public bool ShowingCachedServiceData { get; init; }
    public string CurrentAnnouncement { get; init; } = "No announcement right now.";
    public DateTimeOffset? NextServiceCheckUtc { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? UpdateVersion { get; init; }
    public string WhatChangedText { get; init; } = "What changed notes are not available for this build.";
    public string ReleaseInformation { get; init; } = "No additional release information is available.";
    public IReadOnlyList<AnnouncementHistoryItem> AnnouncementHistory { get; init; } = [];
}
