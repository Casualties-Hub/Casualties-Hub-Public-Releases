using System.Text.Json;
using System.IO;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

public class SettingsService
{
    private static readonly object SettingsSync = new();
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    public SettingsService() { AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CasualtiesHub"); Directory.CreateDirectory(AppDataPath); _settingsPath = Path.Combine(AppDataPath, "Settings.json"); }
    public string AppDataPath { get; }
    public Settings Load()
    {
        lock (SettingsSync)
        {
            if (!File.Exists(_settingsPath))
            {
                var settings = CreateDefaultSettings();
                SaveUnsafe(settings);
                return settings;
            }
            try
            {
                var rawSettings = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<Settings>(rawSettings) ?? new Settings();
                var normalizedSize = Math.Clamp(settings.TextSize, 10, 20);
                var changed = false;
                if (Math.Abs(settings.TextSize - normalizedSize) > double.Epsilon)
                {
                    settings.TextSize = normalizedSize;
                    changed = true;
                }

                using var document = JsonDocument.Parse(rawSettings);

                // Easter eggs became an opt-in preference in v0.0.7. Settings
                // from earlier builds did not contain this field, so make the
                // safe default explicit the first time they are loaded.
                if (!document.RootElement.TryGetProperty(nameof(Settings.EasterEggsPreferenceInitialized), out _))
                {
                    settings.EasterEggsEnabled = false;
                    settings.EasterEggsPreferenceInitialized = true;
                    changed = true;
                }

                // The v0.0.8-pre.6 rebrand replaced the light navigation buttons
                // and crimson body text with the dark theme. Older settings
                // still carry the previous colours, which look wrong against the
                // new shell, so move them across exactly once. Anything the
                // player picks afterwards is left alone.
                if (!settings.RebrandThemeInitialized)
                {
                    var defaults = new Settings();
                    settings.PrimaryTextRed = defaults.PrimaryTextRed;
                    settings.PrimaryTextGreen = defaults.PrimaryTextGreen;
                    settings.PrimaryTextBlue = defaults.PrimaryTextBlue;
                    settings.BackgroundRed = defaults.BackgroundRed;
                    settings.BackgroundGreen = defaults.BackgroundGreen;
                    settings.BackgroundBlue = defaults.BackgroundBlue;
                    settings.SurfaceRed = defaults.SurfaceRed;
                    settings.SurfaceGreen = defaults.SurfaceGreen;
                    settings.SurfaceBlue = defaults.SurfaceBlue;
                    settings.AccentRed = defaults.AccentRed;
                    settings.AccentGreen = defaults.AccentGreen;
                    settings.AccentBlue = defaults.AccentBlue;
                    settings.ThemeColoursInitialized = true;
                    settings.RebrandThemeInitialized = true;
                    changed = true;
                }
                // Presets arrived in v0.0.8-pre.2. Older settings have no slots,
                // so top the list up to a fixed four and keep them addressable
                // by position.
                if (NormalizeCustomPresets(settings)) changed = true;
                if (ReconcileActivePreset(settings)) changed = true;

                if (changed) SaveUnsafe(settings);
                return settings;
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                DebugLogService.Error("Could not load Settings.json; using session defaults", exception);
                // Do not overwrite an unreadable file. The user can still open
                // Settings and choose paths while the underlying issue is fixed.
                return CreateDefaultSettings();
            }
        }
    }

    public void Save(Settings settings)
    {
        lock (SettingsSync)
        {
            settings.TextSize = Math.Clamp(settings.TextSize, 10, 20);
            settings.ThemeColoursInitialized = true;
            // Saving is itself a deliberate colour choice, so the one-time
            // rebrand migration must never run over the top of it afterwards.
            settings.RebrandThemeInitialized = true;
            NormalizeCustomPresets(settings);
            SaveUnsafe(settings);
        }
    }

    private static Settings CreateDefaultSettings()
    {
        var settings = new Settings();
        NormalizeCustomPresets(settings);
        return settings;
    }

    /// <summary>
    /// Stops the preset label claiming "Default" over colours that are not the
    /// stock set. Settings written before presets existed carry the default
    /// label regardless of the colours a player had already chosen.
    /// </summary>
    private static bool ReconcileActivePreset(Settings settings)
    {
        if (!string.Equals(settings.ActiveUiPreset, UiPresetIds.Default, StringComparison.Ordinal)) return false;

        var stock = UiPreset.Stock;
        var matchesStock = settings.PrimaryTextRed == stock.PrimaryTextRed
            && settings.PrimaryTextGreen == stock.PrimaryTextGreen
            && settings.PrimaryTextBlue == stock.PrimaryTextBlue
            && settings.BackgroundRed == stock.BackgroundRed
            && settings.BackgroundGreen == stock.BackgroundGreen
            && settings.BackgroundBlue == stock.BackgroundBlue
            && settings.SurfaceRed == stock.SurfaceRed
            && settings.SurfaceGreen == stock.SurfaceGreen
            && settings.SurfaceBlue == stock.SurfaceBlue
            && settings.AccentRed == stock.AccentRed
            && settings.AccentGreen == stock.AccentGreen
            && settings.AccentBlue == stock.AccentBlue;
        if (matchesStock) return false;

        settings.ActiveUiPreset = UiPresetIds.CustomColours;
        return true;
    }

    /// <summary>
    /// Keeps exactly four custom preset slots so each one can be addressed by
    /// position, whether or not the player has saved anything into it yet.
    /// </summary>
    private static bool NormalizeCustomPresets(Settings settings)
    {
        settings.CustomUiPresets ??= [];
        var changed = false;
        while (settings.CustomUiPresets.Count > UiPresetIds.CustomSlotCount)
        {
            settings.CustomUiPresets.RemoveAt(settings.CustomUiPresets.Count - 1);
            changed = true;
        }
        while (settings.CustomUiPresets.Count < UiPresetIds.CustomSlotCount)
        {
            settings.CustomUiPresets.Add(new UiPreset { Name = $"Preset {settings.CustomUiPresets.Count + 1}" });
            changed = true;
        }
        return changed;
    }

    private void SaveUnsafe(Settings settings)
    {
        // A temporary file plus replace keeps a master refresh or app close from
        // ever seeing a half-written Settings.json.
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, _jsonOptions));
        File.Move(temporaryPath, _settingsPath, true);
    }
}
