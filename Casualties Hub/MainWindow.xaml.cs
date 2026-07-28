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
    private readonly GitHubUpdateService _gitHubUpdateService = new();
    private readonly SupabaseStatusService _supabaseStatusService = new();
    private readonly UpdateInstaller _updateInstaller = new();
    private readonly DispatcherTimer _faceClickTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _developerCommandTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _refreshBlinkTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _cloudStatusTimer = new() { Interval = TimeSpan.FromMinutes(5) };
    private readonly DeveloperCommandService _developerCommandService = new();
    private Color _previousPrimaryTextColor = Colors.White;
    private int _faceClickCount;
    private bool _papaZuckLinkOpenedThisBurst;
    private GitHubUpdate? _availableUpdate;
    private int _refreshBlinkCycles;
    private Page? _currentPage;
    private Button? _activeNavigationButton;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Casualties Hub — 100% Vibe coded by MarlyZ89";
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/CasualtiesHub.png"));
        ApplySavedTextSize();
        ApplySavedTextColor();
        ApplyEasterEggsPreference();
        ContentRendered += (_, _) => { ApplySavedTextSize(); ApplySavedTextColor(); };
        MainFrame.Navigated += (_, _) => Dispatcher.BeginInvoke(ApplySavedTextColor);
        Services.DebugLogService.Activity("Launcher", $"Started Casualties Hub {GetType().Assembly.GetName().Version}.");
        _downloadImportService.Start();
        _faceClickTimer.Tick += FaceClickTimer_Tick;
        _developerCommandTimer.Tick += (_, _) => CheckDeveloperConsoleCommands();
        _developerCommandTimer.Start();
        _refreshBlinkTimer.Tick += RefreshBlinkTimer_Tick;
        _cloudStatusTimer.Tick += async (_, _) => await CheckSupabaseStatusAsync();
        ModService.PluginFilesChanged += PluginFilesChanged;
        ApplyOnlineServicesPreference();
        Loaded += async (_, _) =>
        {
            PromptForOnlineServicesOnFirstLaunch();
            await InitializeCloudFeaturesAsync();
        };
        NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage, Environment.GetCommandLineArgs().Contains("--refresh-metadata", StringComparer.OrdinalIgnoreCase)), DashboardNavButton);
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) => NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage), DashboardNavButton);
    private void Mods_Click(object sender, RoutedEventArgs e) => NavigateTo(new ModsPage(SetStatus), ModsNavButton);
    private void ProtectedFiles_Click(object sender, RoutedEventArgs e) => NavigateTo(new ProtectedFilesPage(SetStatus), ProtectedAssetsNavButton);
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettingsPage();
    private void Credits_Click(object sender, RoutedEventArgs e) => NavigateTo(new CreditsPage(), CreditsNavButton);
    private void HubCenter_Click(object sender, RoutedEventArgs e) => NavigateTo(CreateHubCenterPage(), HubCenterNavButton);

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
        foreach (var button in new[] { DashboardNavButton, ModsNavButton, ProtectedAssetsNavButton, HubCenterNavButton, SettingsNavButton, CreditsNavButton })
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
        await CheckSupabaseStatusAsync();
        await CheckForUpdateAsync();
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
            SetStatus("Online services enabled. Announcements and Hub update checks are available.");
            DebugLogService.Activity("Online services", "Player enabled online services during first-launch setup.");
        }
        else
        {
            SetStatus("Online services remain off. You can enable them later in Hub Center.");
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
            RefreshHubCenterIfOpen();
            return;
        }
        _cloudStatusTimer.Start();
        RefreshHubCenterIfOpen();
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

    private async Task CheckSupabaseStatusAsync()
    {
        if (!_settingsService.Load().HubOnlineServicesEnabled) return;
        var status = await _supabaseStatusService.GetStatusAsync();
        ApplySupabaseStatus(status);
    }

    private async Task CheckSupabaseStatusNowForDeveloperAsync()
    {
        if (!_settingsService.Load().HubOnlineServicesEnabled)
        {
            SetStatus("Hub Online Services are disabled in Settings.");
            return;
        }
        var status = await _supabaseStatusService.GetStatusAsync(true);
        ApplySupabaseStatus(status);
        SetStatus(status.IsOnline ? "Developer Console completed a live Supabase status request." : "Developer Console request used cached Supabase data.");
    }

    private void ApplySupabaseStatus(SupabaseStatus status)
    {
        ShowSupabaseStatus(status);
        RefreshHubCenterIfOpen();
        if (!status.ServerContentChanged) return;

        RefreshCurrentPage("Server update received. Current page refreshed.");
    }

    private void ShowSupabaseStatus(SupabaseStatus status)
    {
        DebugLogService.Activity("Supabase", status.IsOnline
            ? "Hub service status loaded."
            : "Hub service status is using saved data.");
    }

    private HubCenterPage CreateHubCenterPage() => new(
        GetHubCenterState,
        SetHubOnlineServicesEnabledAsync,
        RefreshHubCenterServiceAsync,
        InstallAvailableUpdateAsync,
        InstallLocalUpdateAsync,
        OpenReleaseHistory,
        ConfirmOpenDiscord);

    private HubCenterState GetHubCenterState()
    {
        var settings = _settingsService.Load();
        var status = _supabaseStatusService.LoadCached();
        var manualCheckAvailable = _supabaseStatusService.CanMakeManualCheck(out var nextManualCheckUtc);
        var currentVersion = HubVersion.Current().ToString();
        var releaseNotes = new ReleaseNotesService().GetWhatChanged(currentVersion);
        return new HubCenterState
        {
            CurrentVersion = currentVersion,
            OnlineServicesEnabled = settings.HubOnlineServicesEnabled,
            ServiceOnline = status.IsOnline,
            ServiceInMaintenance = status.IsMaintenance,
            ManualCheckAvailable = manualCheckAvailable,
            NextManualCheckUtc = nextManualCheckUtc,
            ShowingCachedServiceData = status.IsCached,
            CurrentAnnouncement = status.Announcement,
            NextServiceCheckUtc = status.NextCheckUtc,
            UpdateAvailable = _availableUpdate is not null,
            UpdateVersion = _availableUpdate?.Version.ToString(),
            WhatChangedText = releaseNotes,
            ActiveUsersLastTwoHours = status.ActiveUsersLastTwoHours,
            ActiveUsersLastDay = status.ActiveUsersLastDay,
            ActiveUsersLastWeek = status.ActiveUsersLastWeek,
            AnnouncementHistory = (settings.AnnouncementHistory ?? [])
                .OrderByDescending(item => item.ReceivedAtUtc)
                .Take(3)
                .ToList()
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
            await CheckSupabaseStatusAsync();
            await CheckForUpdateAsync();
            SetStatus("Online services enabled. The Hub is checking its saved service schedule.");
        }
        else
        {
            SetStatus("Online services disabled. The Hub will not send server or update requests.");
        }
        RefreshHubCenterIfOpen();
    }

    private async Task RefreshHubCenterServiceAsync()
    {
        if (!_settingsService.Load().HubOnlineServicesEnabled)
        {
            SetStatus("Online services are disabled in Settings.");
            return;
        }

        if (!_supabaseStatusService.CanMakeManualCheck(out var availableAtUtc))
        {
            SetStatus(availableAtUtc is { } availableAt
                ? $"Check now is available at {availableAt.LocalDateTime:t}."
                : "Check now is temporarily unavailable.");
            RefreshHubCenterIfOpen();
            return;
        }

        var status = await _supabaseStatusService.GetManualStatusAsync();
        ApplySupabaseStatus(status);
        await CheckForUpdateAsync();
        SetStatus("Manual Hub service check completed.");
        RefreshHubCenterIfOpen();
    }

    private void RefreshHubCenterIfOpen()
    {
        if (_currentPage is HubCenterPage hubCenter)
            hubCenter.RefreshView();
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
        _developerCommandTimer.Stop();
        _refreshBlinkTimer.Stop();
        _cloudStatusTimer.Stop();
        ModService.PluginFilesChanged -= PluginFilesChanged;
        _downloadImportService.Dispose();
        base.OnClosed(e);
    }

    private void CheckDeveloperConsoleCommands()
    {
        if (!_developerCommandService.TryTake(out var request)) return;
        var command = request.Command;
        _developerCommandService.Acknowledge(request, $"Hub received {command} and is running the requested test.");

        switch (command)
        {
            case "MissingGameLocation":
                DebugLogService.Info("Developer Console simulated a missing Casualties Unknown install.");
                SetStatus("Install cannot be found, manually set game path.");
                var gameDialog = new GameDetectionDialog { Owner = this };
                gameDialog.ShowDialog();
                if (gameDialog.OpenSettingsRequested) OpenSettingsPage();
                break;

            case "MissingPluginsFolder":
                ShowDeveloperFailure("BepInEx Plugins folder cannot be found. Select your Casualties Unknown, BepInEx, or Plugins folder in Settings.");
                break;

            case "MetadataRequestFailed":
                ShowDeveloperFailure("Community metadata could not be loaded. This is a Developer Console test of the normal metadata failure path.");
                break;

            case "ImportFailed":
                ShowDeveloperFailure("The selected mod archive could not be imported. This is a Developer Console test of the normal import failure path.");
                break;

            case "CreateCrashReport":
                var testException = new InvalidOperationException("Developer Console requested a test crash report.");
                DebugLogService.Error("Developer test failure", testException);
                var reportPath = DebugLogService.CreateCrashReport(testException);
                SetStatus(string.IsNullOrWhiteSpace(reportPath) ? "Test crash report could not be created." : "Test crash report created.");
                MessageBox.Show("A test crash report was created without crashing Casualties Hub.", "Developer Console", MessageBoxButton.OK, MessageBoxImage.Information);
                break;

            case "ReloadDashboard":
                NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage, true), DashboardNavButton);
                SetStatus("Developer Console requested a dashboard refresh.");
                break;

            case "ReloadLocalMods":
                NavigateTo(new ModsPage(SetStatus), ModsNavButton);
                SetStatus("Developer Console requested a Local Mods refresh.");
                break;

            case "OpenSettings":
                OpenSettingsPage();
                SetStatus("Developer Console opened Settings.");
                break;

            case "CreateDiagnosticLog":
                var diagnosticPath = DebugLogService.CreateDiagnosticLog();
                SetStatus(string.IsNullOrWhiteSpace(diagnosticPath) ? "Diagnostic log could not be created." : "Diagnostic log created from the last 10 minutes.");
                break;

            case "CheckSupabaseNow":
                _ = CheckSupabaseStatusNowForDeveloperAsync();
                SetStatus("Developer Console is requesting Supabase status now.");
                break;

            case "SimulateSupabaseRateLimit":
                ShowSupabaseStatus(_supabaseStatusService.CreateRateLimitTestStatus());
                SetStatus("Developer Console simulated the one-hour Supabase rate-limit fallback.");
                break;

            case "CheckUpdateFeed":
                _ = CheckForUpdateAsync();
                SetStatus("Developer Console is checking the GitHub update feed for an eligible release.");
                break;

            default:
                DebugLogService.Info($"Developer Console sent an unknown command: {command}");
                _developerCommandService.Acknowledge(request, $"Hub does not recognize the command {command}.");
                break;
        }
    }

    private void ShowDeveloperFailure(string message)
    {
        DebugLogService.Error("Developer Console simulated a failure", new InvalidOperationException(message));
        SetStatus(message);
        MessageBox.Show(message, "Casualties Hub â€” Developer test", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void MasterRefresh_Click(object sender, RoutedEventArgs e) => RestartHub(true);

    /// <summary>
    /// Reloads visible data after a user request or after an allowed server poll
    /// changes the locally cached announcement/compatibility feed. This does not
    /// itself make another Supabase request.
    /// </summary>
    private void RefreshCurrentPage(string completionMessage)
    {
        switch (_currentPage)
        {
            case DashboardPage:
                NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage), DashboardNavButton);
                break;
            case ModsPage:
                NavigateTo(new ModsPage(SetStatus), ModsNavButton);
                break;
            case ProtectedFilesPage:
                NavigateTo(new ProtectedFilesPage(SetStatus), ProtectedAssetsNavButton);
                break;
            case SettingsPage:
                OpenSettingsPage();
                break;
            case CreditsPage:
                NavigateTo(new CreditsPage(), CreditsNavButton);
                break;
            case HubCenterPage hubCenter:
                hubCenter.RefreshView();
                break;
            default:
                NavigateTo(new DashboardPage(SetStatus, OpenSettingsPage), DashboardNavButton);
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
            settings.NextGitHubUpdateCheckUtc = DateTimeOffset.UtcNow.AddHours(6);
            _settingsService.Save(settings);
            if (update is null)
            {
                _availableUpdate = null;
                RefreshHubCenterIfOpen();
                DebugLogService.Activity("Update check", "No newer release was found.");
                return;
            }
            _availableUpdate = update;
            SetStatus($"Casualties Hub update {update.Version} is available.");
            DebugLogService.Activity("Update check", $"Update {update.Version} is available.");
            RefreshHubCenterIfOpen();
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
        Process.Start(new ProcessStartInfo("https://github.com/MarlyZ89/Casualties-Hub-Public-Release/releases") { UseShellExecute = true });
        DebugLogService.Activity("Hub Center", "Opened the GitHub release history.");
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
