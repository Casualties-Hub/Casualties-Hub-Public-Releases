using System.Media;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Casualties_Hub.Models;
using Casualties_Hub.Views;
using Casualties_Hub.Services;

namespace Casualties_Hub;

public partial class MainWindow : Window
{
    private const string DiscordInviteUrl = "https://discord.gg/bzZkjAyu76";
    private const string ReportIssuesInviteUrl = "https://discord.gg/NnJNb7wkc";
    private const string NexusPageUrl = "https://www.nexusmods.com/casualtiesunknown";
    private readonly Services.DownloadImportService _downloadImportService = new();
    private readonly SettingsService _settingsService = new();
    private readonly GameLaunchService _gameLaunchService = new();
    private readonly GitHubHubContentService _hubContentService;
    private readonly AnnouncementHistoryService _announcementHistoryService;
    private readonly DispatcherTimer _faceClickTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _cloudStatusTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _animatedRgbTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private double _animatedRgbHue;
    private Color _previousPrimaryTextColor = Colors.White;
    private int _faceClickCount;
    private bool _papaZuckLinkOpenedThisBurst;
    private Page? _currentPage;
    private Button? _activeNavigationButton;
    private HubContentResult? _hubContentResult;
    private bool _remoteRefreshInProgress;

    public MainWindow()
    {
        InitializeComponent();
        _hubContentService = new GitHubHubContentService(_settingsService);
        _announcementHistoryService = new AnnouncementHistoryService(_settingsService);
        Title = "Casualties Hub — 100% Vibe coded by MarlyZ89";
        SidebarFooterText.Text = $"v{HubVersion.Current()} · Community metadata";
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/CasualtiesHub.png"));
        ApplySavedTextSize();
        ApplySavedTextColor();
        ApplyEasterEggsPreference();
        // Going through the preset keeps a running animation from being reset to
        // the saved colours every time a page loads.
        ContentRendered += (_, _) => { ApplySavedTextSize(); ApplyActiveUiPreset(); };
        MainFrame.Navigated += (_, _) => Dispatcher.BeginInvoke(ApplyActiveUiPreset);
        Services.DebugLogService.Activity("Launcher", $"Started Casualties Hub {GetType().Assembly.GetName().Version}.");
        _downloadImportService.Start();
        _faceClickTimer.Tick += FaceClickTimer_Tick;
        _cloudStatusTimer.Tick += async (_, _) => await RefreshRemoteDataIfEligibleAsync();
        _animatedRgbTimer.Tick += AnimatedRgbTimer_Tick;
        ApplyActiveUiPreset();
        _cloudStatusTimer.Start();
        Loaded += async (_, _) => await InitializeCloudFeaturesAsync();
        Deactivated += async (_, _) => await RefreshRemoteDataIfEligibleAsync();
        NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage, Environment.GetCommandLineArgs().Contains("--refresh-metadata", StringComparer.OrdinalIgnoreCase), RefreshGitHubDataForMetadataPingAsync), DashboardNavButton);
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) => NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage, false, RefreshGitHubDataForMetadataPingAsync), DashboardNavButton);
    private void Mods_Click(object sender, RoutedEventArgs e) => NavigateTo(new ModsPage(SetStatus), ModsNavButton);
    private void Multiplayer_Click(object sender, RoutedEventArgs e) => NavigateTo(new MultiplayerPage(SetStatus), MultiplayerNavButton);
    private void SkinsAndBackups_Click(object sender, RoutedEventArgs e) => NavigateTo(new SkinsAndBackupsPage(SetStatus), SkinsAndBackupsNavButton);
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettingsPage();
    private void HubHome_Click(object sender, RoutedEventArgs e) => NavigateTo(CreateHubHomePage(), HubHomeNavButton);

    /// <summary>
    /// Credits is opened from Hub Home rather than the sidebar, so Hub Home
    /// stays highlighted while the credits page is showing.
    /// </summary>
    private void OpenCreditsPage() => NavigateTo(new CreditsPage(), HubHomeNavButton);

    private void NavigateTo(Page page, Button activeButton)
    {
        ApplySavedTextSize();
        _currentPage = page;
        SetActiveNavigation(activeButton);
        MainFrame.Navigate(page);
        DebugLogService.Activity("Navigation", $"Opened {activeButton.Content}.");
    }

    private void OpenSettingsPage() => NavigateTo(new SettingsPage(SetStatus), SettingsNavButton);

    /// <summary>
    /// Paints the sidebar pills. The open page gets a translucent wash of the
    /// player's accent plus accent-coloured text and dot; the rest stay
    /// transparent and muted. Accent is still the themed brush, so UI presets and
    /// Animated RGB continue to drive the sidebar.
    /// </summary>
    private void SetActiveNavigation(Button activeButton)
    {
        _activeNavigationButton = activeButton;
        var accent = GetThemeBrush("AccentBrush");
        var muted = GetThemeBrush("MutedTextBrush");
        foreach (var button in new[] { DashboardNavButton, ModsNavButton, MultiplayerNavButton, SkinsAndBackupsNavButton, HubHomeNavButton, SettingsNavButton })
        {
            button.Background = Brushes.Transparent;
            button.Foreground = muted;
        }
        activeButton.Background = AccentWashBrush(accent);
        activeButton.Foreground = accent;
    }

    private static Brush AccentWashBrush(Brush accent)
    {
        if (accent is not SolidColorBrush solid) return Brushes.Transparent;
        var color = solid.Color;
        return new SolidColorBrush(Color.FromArgb(0x55, color.R, color.G, color.B));
    }
    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    /// <summary>
    /// Announcements are the only online feature left. The Hub reads the public
    /// HubContent.json and never checks for, downloads, or installs a Hub build.
    /// </summary>
    private async Task InitializeCloudFeaturesAsync()
    {
        _hubContentResult = _hubContentService.LoadCached();
        RefreshHubHomeIfOpen();
        await RefreshRemoteDataIfEligibleAsync();
    }

    /// <summary>Applies the persisted preference for optional Easter eggs.</summary>
    public void ApplyEasterEggsPreference()
    {
        var enabled = _settingsService.Load().EasterEggsEnabled;
        if (!enabled)
        {
            _faceClickTimer.Stop();
            _faceClickCount = 0;
            _papaZuckLinkOpenedThisBurst = false;
            PapaZuck.Visibility = Visibility.Collapsed;
            return;
        }

        PapaZuckScale.ScaleX = 1;
        PapaZuckScale.ScaleY = 1;
        PapaZuck.Opacity = 1;
        PapaZuck.Visibility = Visibility.Visible;
    }

    private async Task RefreshRemoteDataIfEligibleAsync()
    {
        if (IsActive || !_hubContentService.IsCheckDue()) return;
        await RefreshAllRemoteDataAsync();
    }

    private async Task RefreshAllRemoteDataAsync()
    {
        if (_remoteRefreshInProgress) return;
        _remoteRefreshInProgress = true;
        try
        {
            var metadataTask = new UniversalMetadataService(_settingsService).GetModsAsync(true);
            var contentTask = _hubContentService.RefreshAsync();
            await Task.WhenAll(metadataTask, contentTask);
            _hubContentResult = await contentTask;
            RefreshHubHomeIfOpen();
            if (_hubContentResult.ContentChanged)
                SetStatus("Updated GitHub announcements and community metadata were downloaded.");
        }
        finally { _remoteRefreshInProgress = false; }
    }

    private async Task RefreshGitHubDataForMetadataPingAsync()
    {
        if (!_hubContentService.IsCheckDue()) return;
        _hubContentResult = await _hubContentService.RefreshAsync();
        RefreshHubHomeIfOpen();
    }

    private HubHomePage CreateHubHomePage() => new(
        GetHubHomeState,
        OpenReleaseHistory,
        OpenNexusPage,
        ConfirmOpenDiscord,
        OpenCreditsPage);

    private HubHomeState GetHubHomeState()
    {
        var status = _hubContentResult ?? _hubContentService.LoadCached();
        var currentVersion = HubVersion.Current().ToString();
        var releaseNotesService = new ReleaseNotesService();
        return new HubHomeState
        {
            CurrentVersion = currentVersion,
            ServiceOnline = status.IsOnline,
            ShowingCachedServiceData = status.IsCached,
            CurrentAnnouncement = status.Content.CurrentAnnouncement.Message,
            NextServiceCheckUtc = status.NextCheckUtc,
            // Local to the installed build, not the GitHub feed, so these only
            // change when a new build ships.
            WhatChangedText = releaseNotesService.GetWhatChanged(currentVersion),
            ReleaseInformation = releaseNotesService.GetReleaseInformation(currentVersion),
            // History is kept on this PC, so an announcement stays readable here
            // even after a later HubContent.json stops listing it.
            AnnouncementHistory = _announcementHistoryService.Record(status.Content)
        };
    }

    private void RefreshHubHomeIfOpen()
    {
        if (_currentPage is HubHomePage hubHome)
            hubHome.RefreshView();
    }

    /// <summary>Applies the persisted text size after any page or window reload.</summary>
    public void ApplySavedTextSize()
    {
        var savedTextSize = Math.Clamp(_settingsService.Load().TextSize, 10, 20);
        FontSize = savedTextSize;
        Resources["HubTextSize"] = savedTextSize;
        DebugLogService.Activity("Settings", $"Applied saved text size {savedTextSize:0}.");
    }

    /// <summary>
    /// Rebuilds every themed brush from the player's four colours. Pages bind to
    /// these with DynamicResource, so recolouring the background or panels
    /// repaints the whole shell without touching a single page.
    /// </summary>
    public void ApplySavedTextColor()
    {
        var settings = _settingsService.Load();
        foreach (var (key, color) in ThemePalette.Build(settings))
            SetThemeBrush(key, new SolidColorBrush(color));

        var primaryColor = ThemePalette.Text(settings);
        ApplyPrimaryTextColour(Wordmark, this, new SolidColorBrush(primaryColor), _previousPrimaryTextColor);
        _previousPrimaryTextColor = primaryColor;
        if (_activeNavigationButton is not null) SetActiveNavigation(_activeNavigationButton);
    }

    /// <summary>
    /// Starts or stops the animated look. Presets are copied into the saved
    /// colours when applied, so everything else goes through the normal path.
    /// </summary>
    public void ApplyActiveUiPreset()
    {
        if (_settingsService.Load().AnimatedRgbEnabled)
        {
            // The saved colours are applied first so text, background and panels
            // are correct; the timer then only animates the accent over the top.
            ApplySavedTextColor();
            _animatedRgbTimer.Start();
            return;
        }

        _animatedRgbTimer.Stop();
        ApplySavedTextColor();
    }

    private void AnimatedRgbTimer_Tick(object? sender, EventArgs e)
    {
        _animatedRgbHue = (_animatedRgbHue + 2) % 360;
        var color = HueToColor(_animatedRgbHue);
        // Accent only. Page text deliberately keeps the player's saved colour:
        // cycling it meant the readability guard kept swapping the text between
        // white and black as the hue passed each threshold, which read as a
        // flicker. Only these two brushes are swapped because walking the visual
        // tree on every tick would be far too expensive.
        var settings = _settingsService.Load();
        var background = ThemePalette.Background(settings);
        SetThemeBrush("AccentBrush", new SolidColorBrush(color));
        SetThemeBrush("AccentTextBrush", new SolidColorBrush(ThemePalette.ReadableOn(color)));
        // Accent buttons are a dark wash plus an accent label, so both have to
        // follow the hue or the button would stop matching its own text. Shared
        // with the static palette so the two can never drift apart.
        var (soft, onSoft) = ThemePalette.AccentButton(color, background);
        SetThemeBrush("AccentSoftBrush", new SolidColorBrush(soft));
        SetThemeBrush("AccentSoftTextBrush", new SolidColorBrush(onSoft));
        if (_activeNavigationButton is not null) SetActiveNavigation(_activeNavigationButton);
    }

    /// <summary>Converts a hue in degrees to a fully saturated display colour.</summary>
    private static Color HueToColor(double hue)
    {
        const double saturation = 0.85;
        var sector = hue / 60;
        var secondary = saturation * (1 - Math.Abs((sector % 2) - 1));
        var floor = 1 - saturation;
        var (red, green, blue) = (int)sector switch
        {
            0 => (saturation, secondary, 0d),
            1 => (secondary, saturation, 0d),
            2 => (0d, saturation, secondary),
            3 => (0d, secondary, saturation),
            4 => (secondary, 0d, saturation),
            _ => (saturation, 0d, secondary)
        };
        return Color.FromRgb(ToChannel(red + floor), ToChannel(green + floor), ToChannel(blue + floor));
    }

    private static byte ToChannel(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);

    private void SetThemeBrush(string key, Brush brush)
    {
        Application.Current.Resources[key] = brush;
        Resources[key] = brush;
    }

    private Brush GetThemeBrush(string key) => (Brush)Application.Current.Resources[key];

    private static Brush ContrastBrush(Color color)
    {
        var brightness = ((color.R * 299) + (color.G * 587) + (color.B * 114)) / 1000;
        return brightness >= 150 ? Brushes.Black : Brushes.White;
    }

    /// <param name="excluded">
    /// The product wordmark. Its Runs already set their own brushes, but skipping
    /// the element outright makes the intent explicit: branding is never
    /// repainted by a preset, a custom colour, or Animated RGB.
    /// </param>
    private static void ApplyPrimaryTextColour(DependencyObject? excluded, DependencyObject root, Brush brush, Color previousColour)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (ReferenceEquals(child, excluded)) continue;
            if (child is TextBlock textBlock && !IsInsideButton(textBlock) && IsPrimaryTextBrush(textBlock.Foreground, previousColour))
                textBlock.Foreground = brush;
            else if (child is TextBox textBox && IsPrimaryTextBrush(textBox.Foreground, previousColour))
                textBox.Foreground = brush;
            else if (child is PasswordBox passwordBox && IsPrimaryTextBrush(passwordBox.Foreground, previousColour))
                passwordBox.Foreground = brush;
            ApplyPrimaryTextColour(excluded, child, brush, previousColour);
        }
    }

    private static bool IsPrimaryTextBrush(Brush? brush, Color previousColour) => brush is SolidColorBrush solid
        && (solid.Color == Colors.White || solid.Color == previousColour);

    private static bool IsInsideButton(DependencyObject visual)
    {
        DependencyObject? current = visual;
        while (current is not null)
        {
            if (current is Button) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
    protected override void OnClosed(EventArgs e)
    {
        DebugLogService.Activity("Launcher", "Closing Casualties Hub.");
        _cloudStatusTimer.Stop();
        _animatedRgbTimer.Stop();
        _downloadImportService.Dispose();
        base.OnClosed(e);
    }

    private void ConfirmOpenDiscord()
    {
        if (MessageBox.Show("Open the Casualties Hub Discord invite in your browser?", "CH Discord", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            OpenDiscord();
        else
            DebugLogService.Activity("Discord", "User cancelled opening the Discord invite.");
    }

    private void OpenDiscord()
    {
        Process.Start(new ProcessStartInfo(DiscordInviteUrl) { UseShellExecute = true });
        DebugLogService.Activity("Discord", "Opened the Casualties Hub Discord invite in the browser.");
    }

    private void LaunchGame_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DebugLogService.Activity("Game launch", "Requested a Steam launch for Casualties Unknown.");
            _gameLaunchService.LaunchViaSteam(_settingsService.Load());
            SetStatus("Launching Casualties Unknown through Steam.");
        }
        catch (Exception exception)
        {
            Services.DebugLogService.Error("Could not launch Casualties Unknown", exception);
            MessageBox.Show(exception.Message, "Launch game", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ReportIssues_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Open the Casualties Hub report and issue forum in your browser?",
                "Report issues",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            DebugLogService.Activity("Reports", "User cancelled opening the report forum.");
            return;
        }

        Process.Start(new ProcessStartInfo(ReportIssuesInviteUrl) { UseShellExecute = true });
        DebugLogService.Activity("Reports", "Opened the Casualties Hub report and issue forum.");
    }

    private void OpenReleaseHistory()
    {
        Process.Start(new ProcessStartInfo("https://github.com/MarlyZ89/Casualties-Hub-Public-Releases/releases") { UseShellExecute = true });
        DebugLogService.Activity("Hub Home", "Opened the GitHub release history.");
    }

    private void OpenNexusPage()
    {
        Process.Start(new ProcessStartInfo(NexusPageUrl) { UseShellExecute = true });
        DebugLogService.Activity("Hub Home", "Opened the Casualties Unknown Nexus page.");
    }

    private void PapaZuck_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_settingsService.Load().EasterEggsEnabled) return;
        if (e.OriginalSource is System.Windows.Controls.Button) return;
        if (_papaZuckLinkOpenedThisBurst) return;
        _faceClickCount++;
        _faceClickTimer.Stop();
        if (_faceClickCount == 4)
        {
            _faceClickCount = 0;
            _papaZuckLinkOpenedThisBurst = true;
            Process.Start(new ProcessStartInfo("https://www.youtube.com/watch?v=dQw4w9WgXcQ") { UseShellExecute = true });
            DebugLogService.Activity("PapaZuck", "Opened the four-click surprise link.");
            SetStatus("PapaZuck sent you somewhere important.");
            _faceClickTimer.Start();
            return;
        }
        _faceClickTimer.Start();
    }

    private void FaceClickTimer_Tick(object? sender, EventArgs e)
    {
        _faceClickTimer.Stop();
        var clicks = _faceClickCount;
        _faceClickCount = 0;
        _papaZuckLinkOpenedThisBurst = false;
        if (clicks < 3) return;
        var pop = new DoubleAnimation(1, 1.3, TimeSpan.FromMilliseconds(90)) { AutoReverse = true };
        pop.Completed += (_, _) =>
        {
            var disappear = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
            disappear.Completed += (_, _) => { SystemSounds.Beep.Play(); PapaZuck.Visibility = Visibility.Collapsed; };
            PapaZuckScale.BeginAnimation(ScaleTransform.ScaleXProperty, disappear);
            PapaZuckScale.BeginAnimation(ScaleTransform.ScaleYProperty, disappear);
        };
        PapaZuckScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        PapaZuckScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        SetStatus("PapaZuck has disappeared. Restart the Hub to bring him back.");
        DebugLogService.Activity("PapaZuck", "PapaZuck disappeared after the click sequence.");
    }
}
