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
    // Set once when a pre-rebrand settings file is loaded, so the old light
    // buttons and crimson body text are replaced by the dark theme exactly one
    // time and every later choice the player makes is preserved.
    public bool RebrandThemeInitialized { get; set; }
    // The four colours a player can set. Everything else in the shell (button
    // surfaces, borders, muted text, chrome) is derived from these by
    // ThemePalette, so a change to any one of them stays self-consistent.
    // The crimson wordmark is deliberately not driven by any of them.
    public byte PrimaryTextRed { get; set; } = 241;
    public byte PrimaryTextGreen { get; set; } = 239;
    public byte PrimaryTextBlue { get; set; } = 238;
    public byte BackgroundRed { get; set; } = 20;
    public byte BackgroundGreen { get; set; } = 20;
    public byte BackgroundBlue { get; set; } = 20;
    public byte SurfaceRed { get; set; } = 30;
    public byte SurfaceGreen { get; set; } = 30;
    public byte SurfaceBlue { get; set; } = 30;
    public byte AccentRed { get; set; } = 200;
    public byte AccentGreen { get; set; } = 30;
    public byte AccentBlue { get; set; } = 60;
    // Which colours are loaded: the stock set, a saved slot, or the player's own
    // mix. Animated RGB is a separate switch that paints over whichever of those
    // is loaded, so turning it off restores the colours untouched.
    public string ActiveUiPreset { get; set; } = UiPresetIds.Default;
    public bool AnimatedRgbEnabled { get; set; }
    public List<UiPreset> CustomUiPresets { get; set; } = [];
    public string ProtectedFilesPath { get; set; } = "ProtectedFiles";
    // Optional visual/clickable extras are off by default. This flag records
    // that the setting has been initialised so older settings can migrate to
    // the new default without adding a prompt.
    public bool EasterEggsEnabled { get; set; }
    public bool EasterEggsPreferenceInitialized { get; set; }
    public string? CachedCompatibilityRules { get; set; }
    public string? CachedCompatibilityVersion { get; set; }
    public DateTimeOffset? CachedCompatibilityUpdatedAt { get; set; }
    public bool LocalModsShareColumnCollapsed { get; set; }
    public bool LocalModsShareColumnVisible { get; set; }
    public List<string> IgnoredDependencyNames { get; set; } = [];
}
