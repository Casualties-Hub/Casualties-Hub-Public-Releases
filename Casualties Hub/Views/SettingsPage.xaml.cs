using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class SettingsPage : Page
{
    private readonly SettingsService _settingsService = new();
    private readonly NexusApiKeyStore _nexusApiKeyStore;
    private readonly Action<string> _setStatus;
    private bool _isLoadingSettings;
    private bool _isUpdatingTheme;

    public SettingsPage(Action<string> setStatus)
    {
        InitializeComponent();
        _setStatus = setStatus;
        _nexusApiKeyStore = new(_settingsService);
        Refresh();
    }

    private void Refresh()
    {
        _isLoadingSettings = true;
        var settings = _settingsService.Load();
        GamePathBox.Text = settings.GamePath;
        DownloadPathBox.Text = settings.DownloadPath;
        DadipfBox.IsChecked = settings.DisableAutoDeleteImportedParentFiles;
        EasterEggsToggle.IsChecked = settings.EasterEggsEnabled;
        SetThemeControls(settings);
        SetPresetControls(settings);
        var selectedSize = settings.TextSize.ToString("0");
        TextSizeBox.SelectedItem = TextSizeBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), selectedSize, StringComparison.Ordinal));
        NexusApiKeyStatusText.Text = _nexusApiKeyStore.HasKey
            ? "A Nexus API key is saved for this Windows user. Dashboard mod cards can use direct Download."
            : "No Nexus API key is saved. Dashboard mod cards will open the original Nexus Files page.";
        StorageText.Text = $"Hub data is kept locally in:\n{_settingsService.AppDataPath}\n\nRestart the Hub after changing the download inbox so the watcher uses the new folder.";
        _isLoadingSettings = false;
    }

    private void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select your Casualties Unknown game, BepInEx, or Plugins folder" };
        if (dialog.ShowDialog() != true) return;
        var settings = _settingsService.Load();
        settings.GamePath = dialog.FolderName;
        _settingsService.Save(settings);
        DebugLogService.Activity("Settings", "Saved a manually selected game folder.");
        Refresh();
        _setStatus("Game folder updated.");
    }

    private void ChangeDownloadFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select a dedicated Nexus download inbox" };
        if (dialog.ShowDialog() != true) return;
        var settings = _settingsService.Load();
        settings.DownloadPath = dialog.FolderName;
        _settingsService.Save(settings);
        DebugLogService.Activity("Settings", "Saved a new download inbox folder.");
        Refresh();
        _setStatus("Download inbox updated. Restart Hub to apply it.");
    }

    private void DadipfBox_Changed(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        settings.DisableAutoDeleteImportedParentFiles = DadipfBox.IsChecked == true;
        _settingsService.Save(settings);
        DebugLogService.Activity("Settings", settings.DisableAutoDeleteImportedParentFiles ? "Enabled DADIPF." : "Disabled DADIPF.");
        _setStatus(settings.DisableAutoDeleteImportedParentFiles
            ? "DADIPF enabled: imported archives remain in the download inbox."
            : "DADIPF disabled: imported archives are moved to Hub storage after installation.");
    }

    private void EasterEggsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;

        var settings = _settingsService.Load();
        settings.EasterEggsEnabled = EasterEggsToggle.IsChecked == true;
        settings.EasterEggsPreferenceInitialized = true;
        _settingsService.Save(settings);

        if (Application.Current.MainWindow is MainWindow window)
            window.ApplyEasterEggsPreference();

        var state = settings.EasterEggsEnabled ? "enabled" : "disabled";
        DebugLogService.Activity("Settings", $"Easter eggs {state}.");
        _setStatus($"Easter eggs {state}.");
    }

    private void TextSizeBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings) return;
        if (TextSizeBox.SelectedItem is not ComboBoxItem { Tag: string value } || !double.TryParse(value, out var size)) return;
        var settings = _settingsService.Load();
        settings.TextSize = Math.Clamp(size, 10, 20);
        _settingsService.Save(settings);
        DebugLogService.Activity("Settings", $"Saved text size {settings.TextSize:0}.");
        if (Application.Current.MainWindow is MainWindow window) window.ApplySavedTextSize();
        _setStatus($"Text size saved as {settings.TextSize:0}.");
    }

    private void ThemeColourSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingSettings || _isUpdatingTheme) return;
        SaveThemeFromControls();
    }

    private void ThemeHexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingSettings || _isUpdatingTheme || sender is not TextBox { Tag: string category } box) return;
        if (!TryParseHexColour(box.Text, out var color)) return;

        _isUpdatingTheme = true;
        SetThemeSliderValues(category, color);
        _isUpdatingTheme = false;
        SaveThemeFromControls();
    }

    private void RestoreDefaultColours_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        UiPreset.Stock.ApplyColoursTo(settings);
        settings.ActiveUiPreset = UiPresetIds.Default;
        settings.AnimatedRgbEnabled = false;
        _settingsService.Save(settings);

        _isUpdatingTheme = true;
        SetThemeControls(settings);
        _isUpdatingTheme = false;
        SetPresetControls(settings);
        ApplyPreset();
        _setStatus("Default Casualties Hub colours restored.");
        DebugLogService.Activity("Settings", "Restored default Hub colours.");
    }

    private void ApplyUiPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string presetId }) return;
        var settings = _settingsService.Load();
        string message;

        if (UiPresetIds.TryGetCustomSlot(presetId, out var slot))
        {
            var preset = settings.CustomUiPresets[slot - 1];
            if (!preset.IsSaved)
            {
                _setStatus($"Preset {slot} is empty. Set up the look you want, then press Save on that slot.");
                return;
            }
            preset.ApplyTo(settings);
            settings.ActiveUiPreset = presetId;
            settings.AnimatedRgbEnabled = false;
            message = $"Applied {preset.Name}.";
        }
        else if (string.Equals(presetId, UiPresetIds.Default, StringComparison.Ordinal))
        {
            UiPreset.Stock.ApplyColoursTo(settings);
            settings.ActiveUiPreset = UiPresetIds.Default;
            settings.AnimatedRgbEnabled = false;
            message = "Applied the Default colours.";
        }
        else
        {
            // Animated RGB is a switch over the loaded colours, never a
            // replacement for them, so turning it off brings them straight back.
            settings.AnimatedRgbEnabled = !settings.AnimatedRgbEnabled;
            message = settings.AnimatedRgbEnabled
                ? "Animated RGB is on. Turn it off to return to your saved colours."
                : $"Animated RGB is off. Restored {PresetDisplayName(settings, settings.ActiveUiPreset)}.";
        }

        _settingsService.Save(settings);

        _isUpdatingTheme = true;
        SetThemeControls(settings);
        _isUpdatingTheme = false;
        SetPresetControls(settings);
        ApplyPreset();
        if (Application.Current.MainWindow is MainWindow window) window.ApplySavedTextSize();
        _setStatus(message);
        DebugLogService.Activity("Settings", $"UI preset action: {presetId}.");
    }

    private void SaveUiPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string rawSlot } || !int.TryParse(rawSlot, out var slot)) return;
        if (slot < 1 || slot > UiPresetIds.CustomSlotCount) return;

        var settings = _settingsService.Load();
        settings.CustomUiPresets[slot - 1] = UiPreset.Capture(settings, $"Preset {slot}");
        _settingsService.Save(settings);

        SetPresetControls(settings);
        _setStatus($"Saved your current colours and text size to preset {slot}.");
        DebugLogService.Activity("Settings", $"Saved UI preset slot {slot}.");
    }

    private void SetPresetControls(Settings settings)
    {
        var names = new[] { CustomPreset1Name, CustomPreset2Name, CustomPreset3Name, CustomPreset4Name };
        for (var index = 0; index < names.Length; index++)
        {
            var preset = settings.CustomUiPresets[index];
            names[index].Text = preset.IsSaved ? preset.Name : $"Preset {index + 1} (empty)";
        }
        var colours = PresetDisplayName(settings, settings.ActiveUiPreset);
        ActivePresetText.Text = settings.AnimatedRgbEnabled
            ? $"Animated RGB is on, over your {colours} colours. Press it again to turn it off."
            : $"Active colours: {colours}.";
        AnimatedRgbButton.Content = settings.AnimatedRgbEnabled ? "Animated RGB: on" : "Animated RGB";
    }

    private static string PresetDisplayName(Settings settings, string presetId)
    {
        if (string.Equals(presetId, UiPresetIds.CustomColours, StringComparison.Ordinal)) return "custom";
        if (UiPresetIds.TryGetCustomSlot(presetId, out var slot)) return settings.CustomUiPresets[slot - 1].Name;
        return "Default";
    }

    private void ApplyPreset()
    {
        if (Application.Current.MainWindow is MainWindow window) window.ApplyActiveUiPreset();
    }

    private void SaveThemeFromControls()
    {
        var settings = _settingsService.Load();
        settings.PrimaryTextRed = SliderByte(PrimaryTextRedSlider);
        settings.PrimaryTextGreen = SliderByte(PrimaryTextGreenSlider);
        settings.PrimaryTextBlue = SliderByte(PrimaryTextBlueSlider);
        settings.ButtonTextRed = SliderByte(ButtonTextRedSlider);
        settings.ButtonTextGreen = SliderByte(ButtonTextGreenSlider);
        settings.ButtonTextBlue = SliderByte(ButtonTextBlueSlider);
        settings.NavigationSurfaceRed = SliderByte(ButtonSurfaceRedSlider);
        settings.NavigationSurfaceGreen = SliderByte(ButtonSurfaceGreenSlider);
        settings.NavigationSurfaceBlue = SliderByte(ButtonSurfaceBlueSlider);
        settings.AccentRed = SliderByte(AccentRedSlider);
        settings.AccentGreen = SliderByte(AccentGreenSlider);
        settings.AccentBlue = SliderByte(AccentBlueSlider);
        settings.ThemeColoursInitialized = true;
        // These colours are no longer the preset that was loaded, and a hand-picked
        // colour only shows up once the animation stops repainting over it.
        settings.ActiveUiPreset = UiPresetIds.CustomColours;
        settings.AnimatedRgbEnabled = false;
        _settingsService.Save(settings);

        _isUpdatingTheme = true;
        SetThemeControls(settings);
        _isUpdatingTheme = false;
        SetPresetControls(settings);
        ApplyPreset();
        _setStatus("UI colours saved.");
        DebugLogService.Activity("Settings", "Saved UI colour preferences.");
    }

    private void ApplyTheme()
    {
        if (Application.Current.MainWindow is MainWindow window) window.ApplySavedTextColor();
    }

    private void SetThemeControls(Settings settings)
    {
        SetSliders(PrimaryTextRedSlider, PrimaryTextGreenSlider, PrimaryTextBlueSlider, settings.PrimaryTextRed, settings.PrimaryTextGreen, settings.PrimaryTextBlue);
        SetSliders(ButtonTextRedSlider, ButtonTextGreenSlider, ButtonTextBlueSlider, settings.ButtonTextRed, settings.ButtonTextGreen, settings.ButtonTextBlue);
        SetSliders(ButtonSurfaceRedSlider, ButtonSurfaceGreenSlider, ButtonSurfaceBlueSlider, settings.NavigationSurfaceRed, settings.NavigationSurfaceGreen, settings.NavigationSurfaceBlue);
        SetSliders(AccentRedSlider, AccentGreenSlider, AccentBlueSlider, settings.AccentRed, settings.AccentGreen, settings.AccentBlue);
        UpdateThemeControlLabels();
    }

    private void SetThemeSliderValues(string category, Color color)
    {
        switch (category)
        {
            case "PrimaryText": SetSliders(PrimaryTextRedSlider, PrimaryTextGreenSlider, PrimaryTextBlueSlider, color.R, color.G, color.B); break;
            case "ButtonText": SetSliders(ButtonTextRedSlider, ButtonTextGreenSlider, ButtonTextBlueSlider, color.R, color.G, color.B); break;
            case "ButtonSurface": SetSliders(ButtonSurfaceRedSlider, ButtonSurfaceGreenSlider, ButtonSurfaceBlueSlider, color.R, color.G, color.B); break;
            case "Accent": SetSliders(AccentRedSlider, AccentGreenSlider, AccentBlueSlider, color.R, color.G, color.B); break;
            default: return;
        }
        UpdateThemeControlLabels();
    }

    private void UpdateThemeControlLabels()
    {
        UpdateThemeControl(PrimaryTextRedSlider, PrimaryTextGreenSlider, PrimaryTextBlueSlider, PrimaryTextRedValue, PrimaryTextGreenValue, PrimaryTextBlueValue, PrimaryTextHexBox);
        UpdateThemeControl(ButtonTextRedSlider, ButtonTextGreenSlider, ButtonTextBlueSlider, ButtonTextRedValue, ButtonTextGreenValue, ButtonTextBlueValue, ButtonTextHexBox);
        UpdateThemeControl(ButtonSurfaceRedSlider, ButtonSurfaceGreenSlider, ButtonSurfaceBlueSlider, ButtonSurfaceRedValue, ButtonSurfaceGreenValue, ButtonSurfaceBlueValue, ButtonSurfaceHexBox);
        UpdateThemeControl(AccentRedSlider, AccentGreenSlider, AccentBlueSlider, AccentRedValue, AccentGreenValue, AccentBlueValue, AccentHexBox);
    }

    private static void UpdateThemeControl(Slider red, Slider green, Slider blue, TextBlock redValue, TextBlock greenValue, TextBlock blueValue, TextBox hexBox)
    {
        var color = Color.FromRgb(SliderByte(red), SliderByte(green), SliderByte(blue));
        redValue.Text = $"{color.R}";
        greenValue.Text = $"{color.G}";
        blueValue.Text = $"{color.B}";
        hexBox.Text = ToHex(color);
    }

    private static void SetSliders(Slider red, Slider green, Slider blue, byte redValue, byte greenValue, byte blueValue)
    {
        red.Value = redValue;
        green.Value = greenValue;
        blue.Value = blueValue;
    }

    private static byte SliderByte(Slider slider) => (byte)Math.Round(slider.Value);

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static bool TryParseHexColour(string? text, out Color color)
    {
        color = default;
        var value = text?.Trim().TrimStart('#') ?? string.Empty;
        if (value.Length != 6 || !byte.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, null, out var red)
            || !byte.TryParse(value.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var green)
            || !byte.TryParse(value.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue))
            return false;

        color = Color.FromRgb(red, green, blue);
        return true;
    }

    private void SaveNexusApiKey_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NexusApiKeyBox.Password))
        {
            MessageBox.Show("Paste your personal Nexus API key first.", "Nexus Premium API", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            _nexusApiKeyStore.Save(NexusApiKeyBox.Password);
            NexusApiKeyBox.Clear();
            Refresh();
            _setStatus("Nexus Premium API key saved for this Windows user.");
            DebugLogService.Activity("Settings", "Saved a Nexus Premium API key for the current Windows user.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not save Nexus API key", exception);
            MessageBox.Show(exception.Message, "Nexus Premium API", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearNexusApiKey_Click(object sender, RoutedEventArgs e)
    {
        _nexusApiKeyStore.Clear();
        NexusApiKeyBox.Clear();
        Refresh();
        _setStatus("Nexus API key removed.");
        DebugLogService.Activity("Settings", "Removed the stored Nexus Premium API key.");
    }

    private void OpenCrashLogs_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(DebugLogService.CrashReportDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DebugLogService.CrashReportDirectory}\"") { UseShellExecute = true });
        _setStatus("Opened crash report folder.");
        DebugLogService.Activity("Logs", "Opened the crash-report folder.");
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(DebugLogService.LogDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DebugLogService.LogDirectory}\"") { UseShellExecute = true });
        _setStatus("Opened logs folder.");
        DebugLogService.Activity("Logs", "Opened the logs folder.");
    }

    private void CreateLog_Click(object sender, RoutedEventArgs e)
    {
        var path = DebugLogService.CreateDiagnosticLog();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("The diagnostic log could not be created.", "Create Log", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _setStatus("Created diagnostic log from the last 10 minutes of Hub activity.");
        DebugLogService.Activity("Logs", "Created a diagnostic log from recent Hub activity.");
        MessageBox.Show($"Diagnostic log created:\n{path}", "Create Log", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
