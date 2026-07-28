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
                var settings = new Settings();
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

                // Builds released before the first-launch consent prompt did
                // not store this property. Treat those saved preferences as a
                // completed choice instead of surprising an existing player.
                using var document = JsonDocument.Parse(rawSettings);
                if (!document.RootElement.TryGetProperty(nameof(Settings.OnlineServicesChoiceMade), out _))
                {
                    settings.OnlineServicesChoiceMade = true;
                    changed = true;
                }

                // Older builds stored only one white text colour. Move them to
                // the four-part theme once, while preserving all newer choices.
                if (!settings.ThemeColoursInitialized)
                {
                    settings.PrimaryTextRed = 194;
                    settings.PrimaryTextGreen = 31;
                    settings.PrimaryTextBlue = 50;
                    settings.ButtonTextRed = 20;
                    settings.ButtonTextGreen = 20;
                    settings.ButtonTextBlue = 20;
                    settings.NavigationSurfaceRed = 245;
                    settings.NavigationSurfaceGreen = 245;
                    settings.NavigationSurfaceBlue = 245;
                    settings.AccentRed = 194;
                    settings.AccentGreen = 31;
                    settings.AccentBlue = 50;
                    settings.ThemeColoursInitialized = true;
                    changed = true;
                }
                if (changed) SaveUnsafe(settings);
                return settings;
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                DebugLogService.Error("Could not load Settings.json; using session defaults", exception);
                // Do not overwrite an unreadable file. The user can still open
                // Settings and choose paths while the underlying issue is fixed.
                return new Settings();
            }
        }
    }

    public void Save(Settings settings)
    {
        lock (SettingsSync)
        {
            settings.TextSize = Math.Clamp(settings.TextSize, 10, 20);
            settings.ThemeColoursInitialized = true;
            SaveUnsafe(settings);
        }
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
