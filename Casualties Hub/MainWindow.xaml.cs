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
    private readonly Services.DownloadImportService _downloadImportService = new();
    private readonly SettingsService _settingsService = new();
    private readonly GameLaunchService _gameLaunchService = new();
    private readonly GitHubUpdateService _gitHubUpdateService;
    private readonly GitHubHubContentService _hubContentService;
    private readonly AnnouncementHistoryService _announcementHistoryService;
    private readonly UpdateInstaller _updateInstaller = new();
    private readonly DispatcherTimer _faceClickTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _refreshBlinkTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _cloudStatusTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _animatedRgbTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private double _animatedRgbHue;
    private Color _previousPrimaryTextColor = Colors.White;
    private int _faceClickCount;
    private bool _papaZuckLinkOpenedThisBurst;
    private GitHubUpdate? _availableUpdate;
    private int _refreshBlinkCycles;
    private Page? _currentPage;
    private Button? _activeNavigationButton;
    private HubContentResult? _hubContentResult;
    private bool _remoteRefreshInProgress;

    public MainWindow()
    {
        InitializeComponent();
        _gitHubUpdateService = new GitHubUpdateService(_settingsService);
        _hubContentService = new GitHubHubContentService(_settingsService);
        _announcementHistoryService = new AnnouncementHistoryService(_settingsService);
        Title = "Casualties Hub — 100% Vibe coded by MarlyZ89";
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
        _refreshBlinkTimer.Tick += RefreshBlinkTimer_Tick;
        _cloudStatusTimer.Tick += async (_, _) => await RefreshRemoteDataIfEligibleAsync();
        _animatedRgbTimer.Tick += AnimatedRgbTimer_Tick;
        ApplyActiveUiPreset();
        ModService.PluginFilesChanged += PluginFilesChanged;
        ApplyOnlineServicesPreference();
        Loaded += async (_, _) =>
        {
            PromptForOnlineServicesOnFirstLaunch();
            await PromptForStaleUpdateFilesCleanupAsync();
            await InitializeCloudFeaturesAsync();
        };
        Deactivated += async (_, _) => await RefreshRemoteDataIfEligibleAsync();
        NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage, Environment.GetCommandLineArgs().Contains("--refresh-metadata", StringComparer.OrdinalIgnoreCase), RefreshGitHubDataForMetadataPingAsync), DashboardNavButton);
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) => NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage, false, RefreshGitHubDataForMetadataPingAsync), DashboardNavButton);
    private void Mods_Click(object sender, RoutedEventArgs e) => NavigateTo(new ModsPage(SetStatus), ModsNavButton);
    private void ProtectedFiles_Click(object sender, RoutedEventArgs e) => NavigateTo(new ProtectedFilesPage(SetStatus), ProtectedAssetsNavButton);
    private void SkinPreview_Click(object sender, RoutedEventArgs e) => NavigateTo(new SkinPreviewPage(SetStatus), SkinPreviewNavButton);
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

    private void SetActiveNavigation(Button activeButton)
    {
        _activeNavigationButton = activeButton;
        var controlSurface = GetThemeBrush("ControlSurfaceBrush");
        var controlText = GetThemeBrush("ControlTextBrush");
        var accent = GetThemeBrush("AccentBrush");
        var accentText = GetThemeBrush("AccentTextBrush");
        foreach (var button in new[] { DashboardNavButton, ModsNavButton, ProtectedAssetsNavButton, SkinPreviewNavButton, HubHomeNavButton, SettingsNavButton })
        {
            button.Background = controlSurface;
            button.Foreground = controlText;
        }
        activeButton.Background = accent;
        activeButton.Foreground = accentText;
    }
    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private async Task InitializeCloudFeaturesAsync()
    {
        if (!_settingsService.Load().HubOnlineServicesEnabled) return;
        _hubContentResult = _hubContentService.LoadCached();
        RefreshHubHomeIfOpen();
        await RefreshRemoteDataIfEligibleAsync();
    }

    /// <summary>
    /// New installs start offline. Metadata browsing remains available, but
    /// announcements and Hub/GitHub update checks require explicit consent.
    /// </summary>
    private void PromptForOnlineServicesOnFirstLaunch()
    {
        var settings = _settingsService.Load();
        if (settings.OnlineServicesChoiceMade) return;

        var dialog = new OnlineServicesChoiceDialog { Owner = this };
        var enableOnlineServices = dialog.ShowDialog() == true;
        settings.HubOnlineServicesEnabled = enableOnlineServices;
        settings.OnlineServicesChoiceMade = true;
        _settingsService.Save(settings);
        ApplyOnlineServicesPreference();

        if (enableOnlineServices)
        {
            SetStatus("Online services enabled. GitHub announcements and update checks are available.");
            DebugLogService.Activity("Online services", "Player enabled online services during first-launch setup.");
        }
        else
        {
            SetStatus("Online services remain off. You can enable them later in Hub Home.");
            DebugLogService.Activity("Online services", "Player kept online services disabled during first-launch setup.");
        }
    }

    /// <summary>Called by Settings immediately; disabling means no Hub server or GitHub update request is sent.</summary>
    public void ApplyOnlineServicesPreference()
    {
        var enabled = _settingsService.Load().HubOnlineServicesEnabled;
        if (!enabled)
        {
            _cloudStatusTimer.Stop();
            _availableUpdate = null;
            RefreshHubHomeIfOpen();
            return;
        }
        _cloudStatusTimer.Start();
        RefreshHubHomeIfOpen();
    }

    /// <summary>
    /// UpdateInstaller leaves each update's downloaded ZIP and extracted copy behind in Temp,
    /// so this offers to reclaim that space on launch whenever leftovers are actually found.
    /// </summary>
    private async Task PromptForStaleUpdateFilesCleanupAsync()
    {
        var folders = await Task.Run(Services.UpdateCleanupService.ScanStagingFolders);
        if (folders.Count == 0) return;

        var dialog = new UpdateCleanupDialog(folders) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedPaths.Count == 0) return;

        Services.UpdateCleanupService.Delete(dialog.SelectedPaths);
        SetStatus($"Removed {dialog.SelectedPaths.Count} leftover update folder(s).");
        DebugLogService.Activity("Update cleanup", $"Player removed {dialog.SelectedPaths.Count} leftover update folder(s).");
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
        if (_remoteRefreshInProgress || !_settingsService.Load().HubOnlineServicesEnabled) return;
        _remoteRefreshInProgress = true;
        try
        {
            var metadataTask = new UniversalMetadataService(_settingsService).GetModsAsync(true);
            var contentTask = _hubContentService.RefreshAsync();
            var updateTask = CheckForUpdateAsync();
            await Task.WhenAll(metadataTask, contentTask, updateTask);
            _hubContentResult = await contentTask;
            RefreshHubHomeIfOpen();
            if (_hubContentResult.ContentChanged)
                SetStatus("Updated GitHub announcements and community metadata were downloaded.");
        }
        finally { _remoteRefreshInProgress = false; }
    }

    private async Task RefreshGitHubDataForMetadataPingAsync()
    {
        if (!_settingsService.Load().HubOnlineServicesEnabled || !_hubContentService.IsCheckDue()) return;
        _hubContentResult = await _hubContentService.RefreshAsync();
        await CheckForUpdateAsync();
        RefreshHubHomeIfOpen();
    }

    private HubHomePage CreateHubHomePage() => new(
        GetHubHomeState,
        SetHubOnlineServicesEnabledAsync,
        RefreshHubHomeServiceAsync,
        InstallAvailableUpdateAsync,
        InstallLocalUpdateAsync,
        OpenReleaseHistory,
        ConfirmOpenDiscord,
        OpenCreditsPage);

    private HubHomeState GetHubHomeState()
    {
        var settings = _settingsService.Load();
        var status = _hubContentResult ?? _hubContentService.LoadCached();
        var manualCheckAvailable = _hubContentService.IsManualCheckDue();
        var currentVersion = HubVersion.Current().ToString();
        var releaseNotesService = new ReleaseNotesService();
        return new HubHomeState
        {
            CurrentVersion = currentVersion,
            OnlineServicesEnabled = settings.HubOnlineServicesEnabled,
            ServiceOnline = status.IsOnline,
            ManualCheckAvailable = manualCheckAvailable,
            NextManualCheckUtc = _hubContentService.NextManualCheckUtc(),
            ShowingCachedServiceData = status.IsCached,
            CurrentAnnouncement = status.Content.CurrentAnnouncement.Message,
            NextServiceCheckUtc = status.NextCheckUtc,
            UpdateAvailable = _availableUpdate is not null,
            UpdateVersion = _availableUpdate?.Version.ToString(),
            // Local to the installed build, not the GitHub feed, so these only
            // change when a new build ships.
            WhatChangedText = releaseNotesService.GetWhatChanged(currentVersion),
            ReleaseInformation = releaseNotesService.GetReleaseInformation(currentVersion),
            // History is kept on this PC, so an announcement stays readable here
            // even after a later HubContent.json stops listing it.
            AnnouncementHistory = _announcementHistoryService.Record(status.Content)
        };
    }

    private async Task SetHubOnlineServicesEnabledAsync(bool enabled)
    {
        var settings = _settingsService.Load();
        settings.HubOnlineServicesEnabled = enabled;
        _settingsService.Save(settings);
        ApplyOnlineServicesPreference();
        if (enabled)
        {
            _hubContentResult = _hubContentService.LoadCached();
            SetStatus("Online services enabled. Automatic GitHub checks run after the Hub leaves focus.");
        }
        else
        {
            SetStatus("Online services disabled. The Hub will not send server or update requests.");
        }
        RefreshHubHomeIfOpen();
    }

    private async Task RefreshHubHomeServiceAsync()
    {
        if (!_settingsService.Load().HubOnlineServicesEnabled)
        {
            SetStatus("Online services are disabled in Settings.");
            return;
        }

        if (!_hubContentService.IsManualCheckDue())
        {
            var availableAtUtc = _hubContentService.NextManualCheckUtc();
            SetStatus(availableAtUtc is { } availableAt
                ? $"Check now is available at {availableAt.LocalDateTime:T}."
                : "Check now is temporarily unavailable.");
            RefreshHubHomeIfOpen();
            return;
        }

        _hubContentResult = await _hubContentService.RefreshAsync(manual: true);
        await CheckForUpdateAsync();
        SetStatus("GitHub announcement and update check completed.");
        RefreshHubHomeIfOpen();
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

    public void ApplySavedTextColor()
    {
        var settings = _settingsService.Load();
        var primaryColor = Color.FromRgb(settings.PrimaryTextRed, settings.PrimaryTextGreen, settings.PrimaryTextBlue);
        var primaryBrush = new SolidColorBrush(primaryColor);
        SetThemeBrush("PrimaryTextBrush", primaryBrush);
        SetThemeBrush("ControlTextBrush", new SolidColorBrush(Color.FromRgb(settings.ButtonTextRed, settings.ButtonTextGreen, settings.ButtonTextBlue)));
        SetThemeBrush("ControlSurfaceBrush", new SolidColorBrush(Color.FromRgb(settings.NavigationSurfaceRed, settings.NavigationSurfaceGreen, settings.NavigationSurfaceBlue)));

        var accentColor = Color.FromRgb(settings.AccentRed, settings.AccentGreen, settings.AccentBlue);
        SetThemeBrush("AccentBrush", new SolidColorBrush(accentColor));
        SetThemeBrush("AccentTextBrush", ContrastBrush(accentColor));

        ApplyPrimaryTextColour(this, primaryBrush, _previousPrimaryTextColor);
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
        // Only the dynamic brushes are swapped. Walking the visual tree on every
        // tick would be far too expensive, and pages that pick an explicit
        // colour are meant to keep it.
        SetThemeBrush("PrimaryTextBrush", new SolidColorBrush(color));
        SetThemeBrush("AccentBrush", new SolidColorBrush(color));
        SetThemeBrush("AccentTextBrush", ContrastBrush(color));
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

    private static void ApplyPrimaryTextColour(DependencyObject root, Brush brush, Color previousColour)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBlock textBlock && !IsInsideButton(textBlock) && IsPrimaryTextBrush(textBlock.Foreground, previousColour))
                textBlock.Foreground = brush;
            else if (child is TextBox textBox && IsPrimaryTextBrush(textBox.Foreground, previousColour))
                textBox.Foreground = brush;
            else if (child is PasswordBox passwordBox && IsPrimaryTextBrush(passwordBox.Foreground, previousColour))
                passwordBox.Foreground = brush;
            ApplyPrimaryTextColour(child, brush, previousColour);
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
        _refreshBlinkTimer.Stop();
        _cloudStatusTimer.Stop();
        _animatedRgbTimer.Stop();
        ModService.PluginFilesChanged -= PluginFilesChanged;
        _downloadImportService.Dispose();
        base.OnClosed(e);
    }

    private void MasterRefresh_Click(object sender, RoutedEventArgs e) => RestartHub(true);

    /// <summary>
    /// Reloads visible data after a user request or after an allowed server poll
    /// changes the locally cached announcement/compatibility feed. This does not
    /// itself make another network request.
    /// </summary>
    private void RefreshCurrentPage(string completionMessage)
    {
        switch (_currentPage)
        {
            case DashboardPage:
                NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage, false, RefreshGitHubDataForMetadataPingAsync), DashboardNavButton);
                break;
            case ModsPage:
                NavigateTo(new ModsPage(SetStatus), ModsNavButton);
                break;
            case ProtectedFilesPage:
                NavigateTo(new ProtectedFilesPage(SetStatus), ProtectedAssetsNavButton);
                break;
            case SkinPreviewPage:
                NavigateTo(new SkinPreviewPage(SetStatus), SkinPreviewNavButton);
                break;
            case SettingsPage:
                OpenSettingsPage();
                break;
            case CreditsPage:
                OpenCreditsPage();
                break;
            case HubHomePage hubHome:
                hubHome.RefreshView();
                break;
            default:
                NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage, false, RefreshGitHubDataForMetadataPingAsync), DashboardNavButton);
                break;
        }
        SetStatus(completionMessage);
    }
    private void OpenDiscord_Click(object sender, RoutedEventArgs e)
    {
        ConfirmOpenDiscord();
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

    private void PluginFilesChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => PluginFilesChanged(sender, e));
            return;
        }
        _refreshBlinkCycles = 6;
        _refreshBlinkTimer.Start();
    }

    private void RefreshBlinkTimer_Tick(object? sender, EventArgs e)
    {
        if (_refreshBlinkCycles-- <= 0)
        {
            _refreshBlinkTimer.Stop();
            MasterRefreshButton.ClearValue(Control.BackgroundProperty);
            MasterRefreshButton.ClearValue(Control.ForegroundProperty);
            return;
        }
        var highlighted = _refreshBlinkCycles % 2 == 0;
        MasterRefreshButton.Background = highlighted ? new SolidColorBrush(Color.FromRgb(194, 31, 50)) : Brushes.Gold;
        MasterRefreshButton.Foreground = highlighted ? Brushes.White : Brushes.Black;
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

    private async Task CheckForUpdateAsync()
    {
        var settings = _settingsService.Load();
        if (!settings.HubOnlineServicesEnabled) return;
        if (settings.NextGitHubUpdateCheckUtc is { } nextCheck && nextCheck > DateTimeOffset.UtcNow) return;
        try
        {
            DebugLogService.Activity("Update check", "Checking the official GitHub release feed.");
            var current = HubVersion.Current();
            var update = await _gitHubUpdateService.CheckForUpdateAsync(current);
            settings.NextGitHubUpdateCheckUtc = DateTimeOffset.UtcNow.AddMinutes(30);
            _settingsService.Save(settings);
            if (update is null)
            {
                _availableUpdate = null;
                RefreshHubHomeIfOpen();
                DebugLogService.Activity("Update check", "No newer release was found.");
                return;
            }
            _availableUpdate = update;
            SetStatus($"Casualties Hub update {update.Version} is available.");
            DebugLogService.Activity("Update check", $"Update {update.Version} is available.");
            RefreshHubHomeIfOpen();
        }
        catch (Exception exception)
        {
            var retrySettings = _settingsService.Load();
            retrySettings.NextGitHubUpdateCheckUtc = DateTimeOffset.UtcNow.AddMinutes(30);
            _settingsService.Save(retrySettings);
            DebugLogService.Error("GitHub update check failed", exception);
        }
    }

    private async Task InstallAvailableUpdateAsync()
    {
        if (_availableUpdate is null)
        {
            SetStatus("No eligible update is available.");
            return;
        }
        var update = _availableUpdate;
        var response = MessageBox.Show(
            $"Download and install Casualties Hub {update.Version}?\n\nThe Hub will close, replace its program files, and restart.",
            "Install Casualties Hub update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (response != MessageBoxResult.Yes) return;

        try
        {
            SetStatus($"Downloading Casualties Hub {update.Version}.");
            await _updateInstaller.DownloadAndStartAsync(update);
            SetStatus("Update verified. Casualties Hub will restart shortly.");
            Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Automatic update failed", exception);
            MessageBox.Show($"The update was not installed.\n\n{exception.Message}\n\nYou can download it manually from GitHub.", "Update failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            Process.Start(new ProcessStartInfo(update.ReleaseUrl) { UseShellExecute = true });
        }
    }

    private async Task InstallLocalUpdateAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Casualties Hub release ZIP (*.zip)|*.zip",
            Title = "Choose a downloaded Casualties Hub release ZIP"
        };
        if (dialog.ShowDialog(this) != true) return;

        var response = MessageBox.Show(
            "Install this local Casualties Hub release?\n\nThe Hub will close, replace only its own files, and restart. Your game, mods, and protected assets will not be changed.",
            "Install local Hub release",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (response != MessageBoxResult.Yes) return;

        try
        {
            SetStatus("Preparing the local Casualties Hub release.");
            await _updateInstaller.InstallLocalArchiveAndStartAsync(dialog.FileName);
            SetStatus("Local release verified. Casualties Hub will restart shortly.");
            Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Local Hub release installation failed", exception);
            MessageBox.Show($"The local release was not installed.\n\n{exception.Message}", "Install local Hub release", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            RestartHub(true);
        }
    }

    private void RestartHub(bool refreshMetadata = false)
    {
        DebugLogService.Activity("Launcher", refreshMetadata ? "Master refresh requested; restarting and refreshing metadata." : "Restart requested.");
        ApplySavedTextSize();
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
            return;

        _downloadImportService.Dispose();
        Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true, Arguments = refreshMetadata ? "--refresh-metadata" : "" });
        Application.Current.Shutdown();
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
