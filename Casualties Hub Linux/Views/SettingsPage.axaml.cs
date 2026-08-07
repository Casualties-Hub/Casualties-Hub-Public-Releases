using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Folders, appearance, saved looks, the Nexus key, extras, and removal.
/// </summary>
/// <remarks>
/// The game folder picker matters more here than on Windows: if Steam discovery fails on a
/// tester's machine, this is the only way to point the Hub at the game, and without it a failed
/// detection makes the whole app useless to them.
/// </remarks>
public partial class SettingsPage : UserControl
{
    private const int WheelSize = 220;

    /// <summary>Which of the four themed colours the sliders and wheel are editing.</summary>
    private enum ColourTarget { Text, Background, Panels, Accent }

    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly GameInstallDetector _detector = new();
    private readonly NexusApiKeyStore _apiKeyStore;
    private readonly Action<string> _setStatus;

    private bool _loading = true;

    /// <summary>
    /// For Avalonia's XAML loader and the designer, which can only call a parameterless
    /// constructor. The app always uses the overload below so status messages reach the shell.
    /// </summary>
    public SettingsPage() : this(_ => { }) { }

    public SettingsPage(Action<string> setStatus)
    {
        _setStatus = setStatus;
        _apiKeyStore = new NexusApiKeyStore(_settingsService);
        AvaloniaXamlLoader.Load(this);

        BuildChoices();
        WireEvents();
        LoadFromSettings();

        _loading = false;
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private void BuildChoices()
    {
        var targetBox = this.FindControl<ComboBox>("ColourTargetBox")!;
        targetBox.ItemsSource = new[] { "Text", "Background", "Panels", "Accent" };
        targetBox.SelectedIndex = 0;

        var slotBox = this.FindControl<ComboBox>("SaveSlotBox")!;
        slotBox.ItemsSource = Enumerable.Range(1, UiPresetIds.CustomSlotCount)
            .Select(slot => $"Slot {slot}").ToList();
        slotBox.SelectedIndex = 0;

        this.FindControl<Image>("ColourWheelImage")!.Source = ColourWheel.Render(WheelSize);
    }

    private void WireEvents()
    {
        this.FindControl<Button>("BrowseGameButton")!.Click += async (_, _) => await BrowseGameAsync();
        this.FindControl<Button>("DetectGameButton")!.Click += async (_, _) => await DetectAsync();
        this.FindControl<Button>("BrowseDownloadButton")!.Click += async (_, _) => await BrowseDownloadAsync();
        this.FindControl<Button>("ApplyThemeButton")!.Click += (_, _) => ApplyTheme();
        this.FindControl<Button>("ResetThemeButton")!.Click += (_, _) => ResetTheme();
        this.FindControl<Button>("ApplyHexButton")!.Click += (_, _) => ApplyHex();
        this.FindControl<Button>("SaveKeyButton")!.Click += OnSaveKey;
        this.FindControl<Button>("ClearKeyButton")!.Click += OnClearKey;
        this.FindControl<Button>("UninstallButton")!.Click += async (_, _) => await OpenUninstallAsync();
        this.FindControl<Button>("SavePresetButton")!.Click += (_, _) => SavePreset();
        this.FindControl<Button>("OpenLogsButton")!.Click += (_, _) => LinuxShell.OpenFolder(DebugLogService.LogDirectory);
        this.FindControl<Button>("CopyReportButton")!.Click += async (_, _) => await CopyReportAsync();
        this.FindControl<Button>("AnimatedRgbButton")!.Click += (_, _) => ToggleAnimatedRgb();

        this.FindControl<Button>("DefaultPresetButton")!.Click += (_, _) => LoadPreset(UiPresetIds.Default);
        for (var slot = 1; slot <= UiPresetIds.CustomSlotCount; slot++)
        {
            var captured = slot;
            this.FindControl<Button>($"Preset{slot}Button")!.Click += (_, _) => LoadPreset(UiPresetIds.Custom(captured));
        }

        this.FindControl<ComboBox>("ColourTargetBox")!.SelectionChanged += (_, _) => ShowSelectedColour();

        foreach (var name in new[] { "RedSlider", "GreenSlider", "BlueSlider", "ValueSlider" })
            this.FindControl<Slider>(name)!.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == "Value") OnSliderMoved();
            };

        var textSize = this.FindControl<Slider>("TextSizeSlider")!;
        textSize.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != "Value") return;
            this.FindControl<TextBlock>("TextSizeValue")!.Text = ((int)textSize.Value).ToString();
            if (!_loading) SaveScalars();
        };

        this.FindControl<TextBox>("GamePathBox")!.LostFocus += (_, _) => SaveScalars();
        this.FindControl<TextBox>("DownloadPathBox")!.LostFocus += (_, _) => SaveScalars();
        this.FindControl<CheckBox>("DadipfBox")!.IsCheckedChanged += (_, _) => { if (!_loading) SaveScalars(); };
        this.FindControl<CheckBox>("EasterEggsBox")!.IsCheckedChanged += (_, _) => { if (!_loading) SaveScalars(); };
    }

    // --- loading and saving ------------------------------------------------

    private void LoadFromSettings()
    {
        var settings = _settingsService.Load();

        this.FindControl<TextBox>("GamePathBox")!.Text = settings.GamePath;
        this.FindControl<TextBox>("DownloadPathBox")!.Text = settings.DownloadPath;
        this.FindControl<Slider>("TextSizeSlider")!.Value = settings.TextSize;
        this.FindControl<TextBlock>("TextSizeValue")!.Text = ((int)settings.TextSize).ToString();
        this.FindControl<CheckBox>("DadipfBox")!.IsChecked = settings.DisableAutoDeleteImportedParentFiles;
        this.FindControl<CheckBox>("EasterEggsBox")!.IsChecked = settings.EasterEggsEnabled;

        ShowSelectedColour();
        UpdateGamePathStatus(settings);
        UpdateApiKeyStatus();
        UpdatePresetLabel(settings);
        UpdateAnimatedRgbLabel(settings);
        UpdateStorageText(settings);
        this.FindControl<TextBlock>("DiagnosticsText")!.Text = Diagnostics.Build();
    }

    private async Task CopyReportAsync()
    {
        // Avalonia's clipboard is async and hangs off the TopLevel, unlike WPF's static Clipboard.
        var clipboard = Owner?.Clipboard;
        if (clipboard is null)
        {
            _setStatus("No clipboard available in this session.");
            return;
        }

        await clipboard.SetTextAsync(Diagnostics.Build());
        _setStatus("Diagnostics report copied to the clipboard.");
    }

    /// <summary>Saves everything that is not a colour.</summary>
    private void SaveScalars()
    {
        var settings = _settingsService.Load();
        settings.GamePath = this.FindControl<TextBox>("GamePathBox")!.Text?.Trim() ?? "";
        settings.DownloadPath = this.FindControl<TextBox>("DownloadPathBox")!.Text?.Trim() ?? "";
        settings.TextSize = this.FindControl<Slider>("TextSizeSlider")!.Value;
        settings.DisableAutoDeleteImportedParentFiles = this.FindControl<CheckBox>("DadipfBox")!.IsChecked == true;
        settings.EasterEggsEnabled = this.FindControl<CheckBox>("EasterEggsBox")!.IsChecked == true;
        settings.EasterEggsPreferenceInitialized = true;
        _settingsService.Save(settings);

        UpdateGamePathStatus(settings);
        UpdateStorageText(settings);
    }

    private void UpdateGamePathStatus(Settings settings)
    {
        var status = this.FindControl<TextBlock>("GamePathStatus")!;

        if (string.IsNullOrWhiteSpace(settings.GamePath))
        {
            status.Text = "No game folder set.";
            return;
        }

        var plugins = _modService.GetPluginsPath(settings);
        status.Text = _modService.HasConfiguredPluginsFolder(settings)
            ? $"Plugins folder found: {plugins}"
            : $"No BepInEx plugins folder under this path. Expected around: {plugins}";
    }

    /// <summary>Reports how much space the Hub's own data is using.</summary>
    private void UpdateStorageText(Settings settings)
    {
        try
        {
            var root = LinuxPaths.AppDataRoot();
            long total = 0;
            if (Directory.Exists(root))
                total = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);

            var backups = ModService.BackupRoot(settings);
            long backupBytes = 0;
            if (Directory.Exists(backups))
                backupBytes = Directory.EnumerateFiles(backups, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);

            this.FindControl<TextBlock>("StorageText")!.Text =
                $"Hub data: {total / 1024.0 / 1024.0:F1} MB in {root} (backups account for {backupBytes / 1024.0 / 1024.0:F1} MB).";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.FindControl<TextBlock>("StorageText")!.Text = "Hub data size could not be measured.";
        }
    }

    // --- colours -----------------------------------------------------------

    private ColourTarget SelectedTarget =>
        (ColourTarget)Math.Max(this.FindControl<ComboBox>("ColourTargetBox")!.SelectedIndex, 0);

    private static Color Read(Settings settings, ColourTarget target) => target switch
    {
        ColourTarget.Background => Color.FromRgb(settings.BackgroundRed, settings.BackgroundGreen, settings.BackgroundBlue),
        ColourTarget.Panels => Color.FromRgb(settings.SurfaceRed, settings.SurfaceGreen, settings.SurfaceBlue),
        ColourTarget.Accent => Color.FromRgb(settings.AccentRed, settings.AccentGreen, settings.AccentBlue),
        _ => Color.FromRgb(settings.PrimaryTextRed, settings.PrimaryTextGreen, settings.PrimaryTextBlue),
    };

    private static void Write(Settings settings, ColourTarget target, Color colour)
    {
        switch (target)
        {
            case ColourTarget.Background:
                (settings.BackgroundRed, settings.BackgroundGreen, settings.BackgroundBlue) = (colour.R, colour.G, colour.B);
                break;
            case ColourTarget.Panels:
                (settings.SurfaceRed, settings.SurfaceGreen, settings.SurfaceBlue) = (colour.R, colour.G, colour.B);
                break;
            case ColourTarget.Accent:
                (settings.AccentRed, settings.AccentGreen, settings.AccentBlue) = (colour.R, colour.G, colour.B);
                break;
            default:
                (settings.PrimaryTextRed, settings.PrimaryTextGreen, settings.PrimaryTextBlue) = (colour.R, colour.G, colour.B);
                break;
        }
    }

    /// <summary>Pushes the selected colour into the sliders, hex box, swatch and wheel marker.</summary>
    private void ShowSelectedColour()
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            var colour = Read(_settingsService.Load(), SelectedTarget);
            this.FindControl<Slider>("RedSlider")!.Value = colour.R;
            this.FindControl<Slider>("GreenSlider")!.Value = colour.G;
            this.FindControl<Slider>("BlueSlider")!.Value = colour.B;
            this.FindControl<Slider>("ValueSlider")!.Value = ColourWheel.ToHsv(colour).Value * 100;
            ShowColour(colour);
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private void ShowColour(Color colour)
    {
        this.FindControl<Border>("ColourSwatch")!.Background = new SolidColorBrush(colour);
        this.FindControl<TextBox>("ColourHexBox")!.Text = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
        this.FindControl<TextBlock>("RedValue")!.Text = colour.R.ToString();
        this.FindControl<TextBlock>("GreenValue")!.Text = colour.G.ToString();
        this.FindControl<TextBlock>("BlueValue")!.Text = colour.B.ToString();
        this.FindControl<TextBlock>("ValueValue")!.Text = $"{ColourWheel.ToHsv(colour).Value * 100:F0}";

        var point = ColourWheel.Locate(colour, WheelSize);
        var marker = this.FindControl<Ellipse>("ColourWheelMarker")!;
        marker.Margin = new Thickness(point.X + 10 - marker.Width / 2, point.Y + 10 - marker.Height / 2, 0, 0);
    }

    private void OnSliderMoved()
    {
        if (_loading) return;

        var colour = Color.FromRgb(
            (byte)this.FindControl<Slider>("RedSlider")!.Value,
            (byte)this.FindControl<Slider>("GreenSlider")!.Value,
            (byte)this.FindControl<Slider>("BlueSlider")!.Value);

        StoreColour(colour);
    }

    private void ApplyHex()
    {
        var text = this.FindControl<TextBox>("ColourHexBox")!.Text?.Trim().TrimStart('#') ?? "";
        if (text.Length != 6 || !int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out _))
        {
            _setStatus("Enter a colour as six hex digits, for example #C81E3C.");
            return;
        }

        var colour = Color.FromRgb(
            Convert.ToByte(text[..2], 16),
            Convert.ToByte(text.Substring(2, 2), 16),
            Convert.ToByte(text.Substring(4, 2), 16));

        StoreColour(colour);
        ShowSelectedColour();
    }

    /// <summary>Writes a colour into settings and marks the look as hand-mixed.</summary>
    private void StoreColour(Color colour)
    {
        var settings = _settingsService.Load();
        Write(settings, SelectedTarget, colour);
        settings.ThemeColoursInitialized = true;
        // Editing by hand means the look is no longer one of the saved slots.
        settings.ActiveUiPreset = UiPresetIds.CustomColours;
        _settingsService.Save(settings);

        ShowColour(colour);
        UpdatePresetLabel(settings);
    }

    private void OnWheelPressed(object? sender, PointerPressedEventArgs e) => PickFromWheel(e);

    private void OnWheelMoved(object? sender, PointerEventArgs e)
    {
        // Dragging across the wheel updates continuously, matching the Windows behaviour.
        if (e.GetCurrentPoint(this.FindControl<Image>("ColourWheelImage")).Properties.IsLeftButtonPressed)
            PickFromWheel(e);
    }

    private void PickFromWheel(PointerEventArgs e)
    {
        var image = this.FindControl<Image>("ColourWheelImage")!;
        var point = e.GetPosition(image);
        var value = this.FindControl<Slider>("ValueSlider")!.Value / 100.0;

        if (ColourWheel.Sample(point.X, point.Y, WheelSize, value <= 0 ? 1 : value) is not { } colour) return;

        var wasLoading = _loading;
        _loading = true;
        try
        {
            this.FindControl<Slider>("RedSlider")!.Value = colour.R;
            this.FindControl<Slider>("GreenSlider")!.Value = colour.G;
            this.FindControl<Slider>("BlueSlider")!.Value = colour.B;
        }
        finally
        {
            _loading = wasLoading;
        }

        StoreColour(colour);
    }

    private void ApplyTheme()
    {
        var settings = _settingsService.Load();
        settings.TextSize = this.FindControl<Slider>("TextSizeSlider")!.Value;
        settings.ThemeColoursInitialized = true;
        _settingsService.Save(settings);
        ThemeApplier.Apply(settings);
        _setStatus("Theme applied.");
    }

    private void ResetTheme()
    {
        // A fresh Settings carries the stock palette, so this stays in step with the defaults
        // rather than repeating the numbers here.
        UiPreset.Stock.ApplyColoursTo(_settingsService.Load());
        LoadPreset(UiPresetIds.Default);
    }

    // --- saved looks -------------------------------------------------------

    private void LoadPreset(string presetId)
    {
        var settings = _settingsService.Load();

        UiPreset preset;
        if (presetId == UiPresetIds.Default)
        {
            preset = UiPreset.Stock;
        }
        else if (UiPresetIds.TryGetCustomSlot(presetId, out var slot))
        {
            var saved = settings.CustomUiPresets.ElementAtOrDefault(slot - 1);
            if (saved is null || !saved.IsSaved)
            {
                _setStatus($"Slot {slot} is empty. Mix some colours, then press Save to slot.");
                return;
            }
            preset = saved;
        }
        else
        {
            return;
        }

        preset.ApplyColoursTo(settings);
        settings.ActiveUiPreset = presetId;
        _settingsService.Save(settings);

        ThemeApplier.Apply(settings);
        ShowSelectedColour();
        UpdatePresetLabel(settings);
        _setStatus($"Loaded {(presetId == UiPresetIds.Default ? "the default look" : preset.Name)}.");
    }

    private void SavePreset()
    {
        var slot = Math.Max(this.FindControl<ComboBox>("SaveSlotBox")!.SelectedIndex, 0) + 1;
        var settings = _settingsService.Load();

        // The list is sparse until every slot has been written, so pad it first.
        while (settings.CustomUiPresets.Count < UiPresetIds.CustomSlotCount)
            settings.CustomUiPresets.Add(new UiPreset { Name = $"Slot {settings.CustomUiPresets.Count + 1}" });

        settings.CustomUiPresets[slot - 1] = UiPreset.Capture(settings, $"Slot {slot}");
        settings.ActiveUiPreset = UiPresetIds.Custom(slot);
        _settingsService.Save(settings);

        UpdatePresetLabel(settings);
        _setStatus($"Saved the current colours to slot {slot}.");
    }

    private void UpdatePresetLabel(Settings settings)
    {
        var label = settings.ActiveUiPreset switch
        {
            UiPresetIds.Default => "Default",
            UiPresetIds.CustomColours => "Custom (unsaved)",
            var id when UiPresetIds.TryGetCustomSlot(id, out var slot) => $"Slot {slot}",
            _ => "Custom",
        };
        this.FindControl<TextBlock>("ActivePresetText")!.Text = $"Current look: {label}";
    }

    private void ToggleAnimatedRgb()
    {
        var settings = _settingsService.Load();
        settings.AnimatedRgbEnabled = !settings.AnimatedRgbEnabled;
        _settingsService.Save(settings);

        AnimatedRgbDriver.Sync(_settingsService);
        UpdateAnimatedRgbLabel(settings);
        _setStatus(settings.AnimatedRgbEnabled
            ? "Animated RGB on. Your saved colours are kept and restored when you turn it off."
            : "Animated RGB off.");
    }

    private void UpdateAnimatedRgbLabel(Settings settings) =>
        this.FindControl<Button>("AnimatedRgbButton")!.Content =
            settings.AnimatedRgbEnabled ? "Animated RGB: on" : "Animated RGB: off";

    // --- folders -----------------------------------------------------------

    private async Task BrowseGameAsync()
    {
        var owner = Owner;
        if (owner is null) return;

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select your Casualties Unknown folder",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        this.FindControl<TextBox>("GamePathBox")!.Text = path;
        SaveScalars();
        _setStatus("Game folder updated.");
    }

    private async Task BrowseDownloadAsync()
    {
        var owner = Owner;
        if (owner is null) return;

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select your downloads folder",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        this.FindControl<TextBox>("DownloadPathBox")!.Text = path;
        SaveScalars();
    }

    private async Task DetectAsync()
    {
        _setStatus("Scanning Steam libraries...");
        var found = await _detector.FindGameInstallAsync(TimeSpan.FromSeconds(20));

        if (string.IsNullOrWhiteSpace(found))
        {
            _setStatus("No install found.");
            if (Owner is not null)
                await HubDialog.ShowMessageAsync(Owner, "Nothing found",
                    "No Casualties Unknown install turned up in any Steam library. "
                    + "Use Browse to point the Hub at the folder yourself.");
            return;
        }

        this.FindControl<TextBox>("GamePathBox")!.Text = found;
        SaveScalars();
        _setStatus("Game folder detected.");
    }

    // --- Nexus key ---------------------------------------------------------

    private void UpdateApiKeyStatus() =>
        this.FindControl<TextBlock>("ApiKeyStatus")!.Text = _apiKeyStore.HasKey
            ? $"A key is saved. {NexusApiKeyStore.ProtectionDescription}"
            : "No key saved. Downloads will use the free-account flow.";

    private async void OnSaveKey(object? sender, RoutedEventArgs e)
    {
        var box = this.FindControl<TextBox>("ApiKeyBox")!;
        var key = box.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(key))
        {
            if (Owner is not null)
                await HubDialog.ShowMessageAsync(Owner, "Nothing to save", "Paste your personal API key first.");
            return;
        }

        try
        {
            _apiKeyStore.Save(key);
            box.Text = "";
            UpdateApiKeyStatus();
            _setStatus("Nexus API key saved.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not save the Nexus API key", exception);
            if (Owner is not null)
                await HubDialog.ShowMessageAsync(Owner, "Could not save the key", exception.Message);
        }
    }

    private async void OnClearKey(object? sender, RoutedEventArgs e)
    {
        if (Owner is null) return;
        if (!await HubDialog.ConfirmAsync(Owner, "Remove the saved key?",
                "The stored Nexus API key will be deleted from this machine.",
                confirm: "Remove", destructive: true))
            return;

        _apiKeyStore.Clear();
        UpdateApiKeyStatus();
        _setStatus("Nexus API key removed.");
    }

    // --- removal -----------------------------------------------------------

    private async Task OpenUninstallAsync()
    {
        var owner = Owner;
        if (owner is null) return;

        var dialog = new UninstallDialog(_settingsService);
        await dialog.ShowDialog(owner);

        // The helper script waits for this process to exit before deleting anything, so the app
        // has to close for the removal to actually happen.
        if (dialog.Confirmed
            && Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            DebugLogService.Activity("Uninstall", "Shutting down so the removal helper can run.");
            desktop.Shutdown();
        }
    }
}
