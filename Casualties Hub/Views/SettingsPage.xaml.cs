using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class SettingsPage : Page
{
    private readonly SettingsService _settingsService = new();
    private readonly NexusAuthService _nexusAuthService;
    private readonly Action<string> _setStatus;
    private bool _isLoadingSettings;
    private bool _isUpdatingTheme;

    public SettingsPage(Action<string> setStatus)
    {
        InitializeComponent();
        _setStatus = setStatus;
        _nexusAuthService = new(_settingsService);
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
        LoadDraftFromSettings();
        SetPresetControls(settings);
        var selectedSize = settings.TextSize.ToString("0");
        TextSizeBox.SelectedItem = TextSizeBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), selectedSize, StringComparison.Ordinal));
        NexusSignInStatusText.Text = _nexusAuthService.IsSignedIn
            ? $"Signed in to Nexus Mods as {_nexusAuthService.Username}. Dashboard mod cards can use direct Download."
            : "Not signed in to Nexus Mods. Dashboard mod cards will open the original Nexus Files page.";
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

    // ----- Custom colours -----
    //
    // One editor drives whichever of the four themeable colours is selected.
    // Nothing is written to Settings until Apply, so a half-typed hex value or a
    // stray keystroke never repaints the application.

    /// <summary>The colour currently being edited, before Apply is pressed.</summary>
    private Color _draftColour;

    private string SelectedColourTarget =>
        (ColourTargetBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Accent";

    private void ToggleCustomColours_Click(object sender, RoutedEventArgs e)
    {
        var opening = CustomColourPanel.Visibility != Visibility.Visible;
        CustomColourPanel.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        if (opening) LoadDraftFromSettings();
    }

    private void ColourTarget_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings || !IsLoaded) return;
        LoadDraftFromSettings();
    }

    /// <summary>Fills the editor from whatever the selected element is set to now.</summary>
    private void LoadDraftFromSettings()
    {
        var settings = _settingsService.Load();
        // Switching element starts from the plain swatch again, so the panel
        // always answers "what is this set to" before offering to change it.
        ShowColourWheel(false);
        SetDraft(SelectedColourTarget switch
        {
            "Background" => ThemePalette.Background(settings),
            "Surface" => ThemePalette.Surface(settings),
            "Text" => ThemePalette.Text(settings),
            _ => ThemePalette.Accent(settings)
        });
    }

    private void SetDraft(Color color) => SetDraft(color, syncBrightness: true);

    /// <param name="syncBrightness">
    /// False while the brightness slider itself is driving the change, so moving
    /// it does not immediately rewrite its own value and fight the drag.
    /// </param>
    private void SetDraft(Color color, bool syncBrightness)
    {
        _draftColour = color;
        _isUpdatingTheme = true;
        ColourHexBox.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";
        ColourRedSlider.Value = color.R;
        ColourGreenSlider.Value = color.G;
        ColourBlueSlider.Value = color.B;
        ColourRedValue.Text = color.R.ToString();
        ColourGreenValue.Text = color.G.ToString();
        ColourBlueValue.Text = color.B.ToString();
        ColourSwatch.Fill = new SolidColorBrush(color);

        var (hue, saturation, value) = ToHsv(color);
        if (syncBrightness) ColourValueSlider.Value = value * 100;
        _wheelValue = syncBrightness ? value : _wheelValue;
        _isUpdatingTheme = false;

        // Repainting the wheel costs a full bitmap, so only do it while it is
        // actually on screen.
        if (ColourWheelImage.Visibility == Visibility.Visible)
        {
            RenderColourWheel();
            PositionWheelMarker(hue, saturation);
        }
    }

    /// <summary>
    /// The slot shows the current colour until the player asks for the wheel, so
    /// the panel opens on "here is your colour" rather than a picker.
    /// </summary>
    private void ShowColourWheel(bool show)
    {
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ColourWheelImage.Visibility = visibility;
        ColourWheelMarker.Visibility = visibility;
        // Brightness only means something once there is a wheel to modulate.
        BrightnessPanel.Visibility = visibility;
        ColourSwatch.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        ColourSlotHint.Text = show
            ? "Click or drag on the wheel."
            : "Click the circle to open the colour wheel.";

        if (!show) return;
        var (hue, saturation, _) = ToHsv(_draftColour);
        RenderColourWheel();
        PositionWheelMarker(hue, saturation);
    }

    private void ColourSwatch_Click(object sender, MouseButtonEventArgs e) => ShowColourWheel(true);

    // ----- Colour wheel -----
    //
    // Hue is the angle around the wheel, saturation the distance from the middle.
    // Brightness is a separate slider; the wheel is redrawn at the current
    // brightness so what you see is what you get.

    private const int WheelSize = 86;
    private double _wheelValue = 1;
    private bool _draggingWheel;

    private void RenderColourWheel()
    {
        var radius = WheelSize / 2.0;
        var pixels = new byte[WheelSize * WheelSize * 4];
        for (var y = 0; y < WheelSize; y++)
        {
            for (var x = 0; x < WheelSize; x++)
            {
                var dx = (x + 0.5) - radius;
                var dy = (y + 0.5) - radius;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var index = ((y * WheelSize) + x) * 4;
                if (distance > radius)
                {
                    // Outside the circle stays fully transparent, which is what
                    // makes the control read as round rather than a square image.
                    pixels[index + 3] = 0;
                    continue;
                }

                var hue = ((Math.Atan2(dy, dx) * 180 / Math.PI) + 360) % 360;
                var color = FromHsv(hue, Math.Min(distance / radius, 1), _wheelValue);
                pixels[index] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                // Feather the last pixel so the rim is not visibly stair-stepped.
                pixels[index + 3] = (byte)(distance > radius - 1 ? 255 * (radius - distance) : 255);
            }
        }

        var bitmap = new WriteableBitmap(WheelSize, WheelSize, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, WheelSize, WheelSize), pixels, WheelSize * 4, 0);
        ColourWheelImage.Source = bitmap;
    }

    private void PositionWheelMarker(double hue, double saturation)
    {
        var radius = WheelSize / 2.0;
        var angle = hue * Math.PI / 180;
        var x = radius + (Math.Cos(angle) * saturation * radius);
        var y = radius + (Math.Sin(angle) * saturation * radius);
        ColourWheelMarker.Margin = new Thickness(x - 6, y - 6, 0, 0);
    }

    private void ColourWheel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingWheel = true;
        ColourWheelImage.CaptureMouse();
        PickFromWheel(e.GetPosition(ColourWheelImage));
    }

    private void ColourWheel_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingWheel) PickFromWheel(e.GetPosition(ColourWheelImage));
    }

    private void ColourWheel_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingWheel = false;
        ColourWheelImage.ReleaseMouseCapture();
    }

    private void PickFromWheel(Point position)
    {
        var radius = WheelSize / 2.0;
        var dx = position.X - radius;
        var dy = position.Y - radius;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var hue = ((Math.Atan2(dy, dx) * 180 / Math.PI) + 360) % 360;
        // Dragging past the rim keeps picking the outer edge rather than stopping.
        var saturation = Math.Min(distance / radius, 1);
        SetDraft(FromHsv(hue, saturation, _wheelValue), syncBrightness: false);
    }

    private void ColourValue_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingSettings || _isUpdatingTheme || !IsLoaded) return;
        _wheelValue = ColourValueSlider.Value / 100;
        var (hue, saturation, _) = ToHsv(_draftColour);
        SetDraft(FromHsv(hue, saturation, _wheelValue), syncBrightness: false);
    }

    private static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double hue = 0;
        if (delta > 0)
        {
            if (max == r) hue = 60 * (((g - b) / delta) % 6);
            else if (max == g) hue = 60 * (((b - r) / delta) + 2);
            else hue = 60 * (((r - g) / delta) + 4);
        }
        if (hue < 0) hue += 360;
        return (hue, max <= 0 ? 0 : delta / max, max);
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var secondary = chroma * (1 - Math.Abs(((hue / 60) % 2) - 1));
        var offset = value - chroma;
        var (r, g, b) = (int)(hue / 60) switch
        {
            0 => (chroma, secondary, 0d),
            1 => (secondary, chroma, 0d),
            2 => (0d, chroma, secondary),
            3 => (0d, secondary, chroma),
            4 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary)
        };
        return Color.FromRgb(Channel(r + offset), Channel(g + offset), Channel(b + offset));
    }

    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);

    private void ColourHex_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingSettings || _isUpdatingTheme) return;
        if (!TryParseHexColour(ColourHexBox.Text, out var color)) return;
        SetDraft(color);
    }

    private void ColourChannel_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingSettings || _isUpdatingTheme || !IsLoaded) return;
        SetDraft(Color.FromRgb(
            SliderByte(ColourRedSlider),
            SliderByte(ColourGreenSlider),
            SliderByte(ColourBlueSlider)));
    }

    private static byte SliderByte(Slider slider) => (byte)Math.Clamp(Math.Round(slider.Value), 0, 255);

    private void ApplyCustomColour_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        var color = _draftColour;
        switch (SelectedColourTarget)
        {
            case "Background":
                (settings.BackgroundRed, settings.BackgroundGreen, settings.BackgroundBlue) = (color.R, color.G, color.B);
                break;
            case "Surface":
                (settings.SurfaceRed, settings.SurfaceGreen, settings.SurfaceBlue) = (color.R, color.G, color.B);
                break;
            case "Text":
                (settings.PrimaryTextRed, settings.PrimaryTextGreen, settings.PrimaryTextBlue) = (color.R, color.G, color.B);
                break;
            default:
                (settings.AccentRed, settings.AccentGreen, settings.AccentBlue) = (color.R, color.G, color.B);
                break;
        }
        // These are no longer the preset that was loaded, and a hand-picked colour
        // only shows up once the animation stops repainting over it.
        settings.ActiveUiPreset = UiPresetIds.CustomColours;
        settings.AnimatedRgbEnabled = false;
        _settingsService.Save(settings);

        SetPresetControls(settings);
        ApplyPreset();
        _setStatus($"{TargetLabel(SelectedColourTarget)} colour applied.");
        DebugLogService.Activity("Settings", $"Applied a custom {SelectedColourTarget} colour.");
    }

    private void ResetCustomColour_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        var stock = UiPreset.Stock;
        switch (SelectedColourTarget)
        {
            case "Background":
                (settings.BackgroundRed, settings.BackgroundGreen, settings.BackgroundBlue) = (stock.BackgroundRed, stock.BackgroundGreen, stock.BackgroundBlue);
                break;
            case "Surface":
                (settings.SurfaceRed, settings.SurfaceGreen, settings.SurfaceBlue) = (stock.SurfaceRed, stock.SurfaceGreen, stock.SurfaceBlue);
                break;
            case "Text":
                (settings.PrimaryTextRed, settings.PrimaryTextGreen, settings.PrimaryTextBlue) = (stock.PrimaryTextRed, stock.PrimaryTextGreen, stock.PrimaryTextBlue);
                break;
            default:
                (settings.AccentRed, settings.AccentGreen, settings.AccentBlue) = (stock.AccentRed, stock.AccentGreen, stock.AccentBlue);
                break;
        }
        settings.AnimatedRgbEnabled = false;
        _settingsService.Save(settings);

        // Reloading rather than reusing the reconcile result keeps the "Default"
        // label honest once the last hand-picked colour has been put back.
        var reloaded = _settingsService.Load();
        LoadDraftFromSettings();
        SetPresetControls(reloaded);
        ApplyPreset();
        _setStatus($"{TargetLabel(SelectedColourTarget)} colour reset to the Casualties Hub default.");
        DebugLogService.Activity("Settings", $"Reset the {SelectedColourTarget} colour to default.");
    }

    private static string TargetLabel(string target) => target switch
    {
        "Background" => "Background",
        "Surface" => "Panels and cards",
        "Text" => "Text",
        _ => "Accent"
    };

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

        LoadDraftFromSettings();
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

    private async void SignInToNexus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) button.IsEnabled = false;
        _setStatus("Opening your browser to sign in to Nexus Mods…");
        try
        {
            var result = await _nexusAuthService.SignInAsync();
            if (result.Success)
            {
                Refresh();
                _setStatus($"Signed in to Nexus Mods as {result.Username}.");
                DebugLogService.Activity("Settings", $"Signed in to Nexus Mods as {result.Username}.");
            }
            else
            {
                MessageBox.Show(result.Error, "Nexus sign-in", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Nexus sign-in failed", exception);
            MessageBox.Show(exception.Message, "Nexus sign-in", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (sender is Button button2) button2.IsEnabled = true;
        }
    }

    private void SignOutOfNexus_Click(object sender, RoutedEventArgs e)
    {
        _nexusAuthService.SignOut();
        Refresh();
        _setStatus("Signed out of Nexus Mods.");
        DebugLogService.Activity("Settings", "Signed out of Nexus Mods.");
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

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var items = UninstallService.GetItems(_settingsService);
        var dialog = new UninstallDialog(items) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        DebugLogService.Activity("Uninstall", $"Player confirmed removal of: {string.Join(", ", dialog.SelectedItems.Select(item => item.Title))}.");
        UninstallService.BeginUninstall(dialog.SelectedItems);
        Application.Current.Shutdown();
    }
}
