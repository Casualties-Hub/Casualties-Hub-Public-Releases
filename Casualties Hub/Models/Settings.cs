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
    // Which colours are loaded: the stock set, a saved slot, or the player's own
    // mix. Animated RGB is a separate switch that paints over whichever of those
    // is loaded, so turning it off restores the colours untouched.
    public string ActiveUiPreset { get; set; } = UiPresetIds.Default;
    public bool AnimatedRgbEnabled { get; set; }
    public List<UiPreset> CustomUiPresets { get; set; } = [];
    public string ProtectedFilesPath { get; set; } = "ProtectedFiles";
    // Online services are an opt-in feature for new installs. Legacy settings
    // are marked as already answered during loading so an update never changes
    // an existing player's preference or shows an unexpected prompt.
    public bool HubOnlineServicesEnabled { get; set; }
    public bool OnlineServicesChoiceMade { get; set; }
    // Optional visual/clickable extras are off by default. This flag records
    // that the setting has been initialised so older settings can migrate to
    // the new default without adding a prompt.
    public bool EasterEggsEnabled { get; set; }
    public bool EasterEggsPreferenceInitialized { get; set; }
    public DateTimeOffset? NextGitHubUpdateCheckUtc { get; set; }
    public string? CachedCompatibilityRules { get; set; }
    public string? CachedCompatibilityVersion { get; set; }
    public DateTimeOffset? CachedCompatibilityUpdatedAt { get; set; }
    public bool LocalModsShareColumnCollapsed { get; set; }
    public bool LocalModsShareColumnVisible { get; set; }
    public List<string> IgnoredDependencyNames { get; set; } = [];
}
