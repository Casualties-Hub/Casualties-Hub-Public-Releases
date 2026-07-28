using System.IO;

namespace Casualties_Hub.Models;

public class Settings
{
    public string GamePath { get; set; } = "";
    public string BackupPath { get; set; } = "Backups";
    public string DownloadPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    public bool DisableAutoDeleteImportedParentFiles { get; set; }
    public double TextSize { get; set; } = 14;
    // Theme colours are intentionally split by role so light navigation buttons
    // stay readable without making the dark pages use black text.
    public bool ThemeColoursInitialized { get; set; }
    // The normal page-text default matches the crimson used by the Hub wordmark.
    // The wordmark itself is deliberately not driven by this setting.
    public byte PrimaryTextRed { get; set; } = 194;
    public byte PrimaryTextGreen { get; set; } = 31;
    public byte PrimaryTextBlue { get; set; } = 50;
    public byte ButtonTextRed { get; set; } = 20;
    public byte ButtonTextGreen { get; set; } = 20;
    public byte ButtonTextBlue { get; set; } = 20;
    public byte NavigationSurfaceRed { get; set; } = 245;
    public byte NavigationSurfaceGreen { get; set; } = 245;
    public byte NavigationSurfaceBlue { get; set; } = 245;
    public byte AccentRed { get; set; } = 194;
    public byte AccentGreen { get; set; } = 31;
    public byte AccentBlue { get; set; } = 50;
    public string ProtectedFilesPath { get; set; } = "ProtectedFiles";
    public string? CachedAnnouncement { get; set; }
    public string? CachedAnnouncementId { get; set; }
    public DateTimeOffset? CachedAnnouncementUpdatedAt { get; set; }
    public int CachedActiveUsersLastTwoHours { get; set; }
    public int CachedActiveUsersLastDay { get; set; }
    public int CachedActiveUsersLastWeek { get; set; }
    public List<AnnouncementHistoryItem> AnnouncementHistory { get; set; } = [];
    public DateTimeOffset? LastSupabaseCheckUtc { get; set; }
    public DateTimeOffset? NextSupabaseCheckUtc { get; set; }
    public DateTimeOffset? NextManualSupabaseCheckUtc { get; set; }
    // Random anonymous identifier created locally on first online-service use.
    // It is only used for aggregate activity counts and is never shown in the UI.
    public string? InstallationId { get; set; }
    // Local guard in addition to the Supabase/IP-side limit. A forced developer
    // check is still one outgoing request and cannot spam the public endpoint.
    public List<DateTimeOffset> SupabaseRequestHistoryUtc { get; set; } = [];
    // Online services are an opt-in feature for new installs. Legacy settings
    // are marked as already answered during loading so an update never changes
    // an existing player's preference or shows an unexpected prompt.
    public bool HubOnlineServicesEnabled { get; set; }
    public bool OnlineServicesChoiceMade { get; set; }
    public DateTimeOffset? NextGitHubUpdateCheckUtc { get; set; }
    public string? CachedCompatibilityRules { get; set; }
    public string? CachedCompatibilityVersion { get; set; }
    public DateTimeOffset? CachedCompatibilityUpdatedAt { get; set; }
    public bool LocalModsShareColumnCollapsed { get; set; }
    public bool LocalModsShareColumnVisible { get; set; }
    public List<string> IgnoredDependencyNames { get; set; } = [];
}
