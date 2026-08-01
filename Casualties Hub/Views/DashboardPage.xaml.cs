using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class DashboardPage : Page
{
    private const int PageSize = 50;
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly ProtectedFilesService _protectedFilesService;
    private readonly UniversalMetadataService _metadataService;
    private readonly NexusApiKeyStore _nexusApiKeyStore;
    private readonly NexusDownloadService _nexusDownloadService = new();
    private readonly GameInstallDetector _gameInstallDetector = new();
    private readonly Action<string> _setStatus;
    private readonly Action? _openSettings;
    private readonly Func<Task>? _refreshGitHubData;
    private static bool _missingGamePromptShown;
    private bool _forceMetadataRefresh;
    private IReadOnlyList<MetadataMod> _allMods = [];
    private int _currentPage = 1;

    public DashboardPage(Action<string> setStatus, Action? openSettings = null, bool forceMetadataRefresh = false, Func<Task>? refreshGitHubData = null)
    {
        InitializeComponent();
        _setStatus = setStatus;
        _openSettings = openSettings;
        _refreshGitHubData = refreshGitHubData;
        _forceMetadataRefresh = forceMetadataRefresh;
        _protectedFilesService = new(_settingsService, _modService);
        _metadataService = new(_settingsService);
        _nexusApiKeyStore = new(_settingsService);
        RefreshLocalCounts();
        Loaded += DashboardPage_Loaded;
        Unloaded += DashboardPage_Unloaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        DebugLogService.Activity("Nexus Dashboard", "Dashboard loaded.");
        ModService.PluginFilesChanged += PluginFilesChanged;
        await EnsureGamePathAsync();
        await LoadMetadataAsync(_forceMetadataRefresh);
        _forceMetadataRefresh = false;
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e) => ModService.PluginFilesChanged -= PluginFilesChanged;

    private void PluginFilesChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => PluginFilesChanged(sender, e));
            return;
        }
        RefreshLocalCounts();
        if (_allMods.Count > 0)
        {
            UpdateLocalModInfo();
            ApplyFilters();
        }
        _setStatus("Local mod files changed. Dashboard status refreshed.");
        DebugLogService.Activity("Nexus Dashboard", "Detected local plugin changes and refreshed installed-mod status.");
    }

    private void RefreshLocalCounts()
    {
        var settings = _settingsService.Load();
        GamePathText.Text = string.IsNullOrWhiteSpace(settings.GamePath) ? "Not configured" : settings.GamePath;
        GameFolderCard.Visibility = _modService.HasConfiguredGameFolder(settings) ? Visibility.Collapsed : Visibility.Visible;
        ModCountText.Text = _modService.GetInstalledMods(settings).Count.ToString();
    }

    private async Task EnsureGamePathAsync()
    {
        var settings = _settingsService.Load();
        if (_modService.HasConfiguredGameFolder(settings))
        {
            GameFolderCard.Visibility = Visibility.Collapsed;
            DebugLogService.Activity("Game detection", "Using the saved Casualties Unknown folder.");
            return;
        }

        DebugLogService.Activity("Game detection", "No valid saved game folder; starting automatic detection.");
        GamePathText.Text = "Looking for Casualties Unknown Demo…";
        var detectedPath = await _gameInstallDetector.FindGameInstallAsync(TimeSpan.FromSeconds(10));
        if (detectedPath is null)
        {
            DebugLogService.Activity("Game detection", "Automatic detection did not find Casualties Unknown within 10 seconds.");
            GameFolderCard.Visibility = Visibility.Visible;
            GamePathText.Text = "Install cannot be found, manually set game path";
            _setStatus("Install cannot be found, manually set game path.");
            ShowMissingGamePrompt();
            return;
        }

        settings.GamePath = detectedPath;
        _settingsService.Save(settings);
        DebugLogService.Activity("Game detection", "Automatically detected and saved the Casualties Unknown folder.");
        RefreshLocalCounts();
        GameFolderCard.Visibility = Visibility.Collapsed;
        _setStatus("Game install detected automatically.");
    }

    private void ShowMissingGamePrompt()
    {
        if (_missingGamePromptShown) return;
        _missingGamePromptShown = true;

        var dialog = new GameDetectionDialog { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        if (dialog.OpenSettingsRequested)
            _openSettings?.Invoke();
    }

    private void ChooseGameFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select your Casualties Unknown game, BepInEx, or Plugins folder" };
        if (dialog.ShowDialog() != true) return;
        var settings = _settingsService.Load();
        settings.GamePath = dialog.FolderName;
        _settingsService.Save(settings);
        DebugLogService.Activity("Game detection", "User manually selected a game folder.");
        RefreshLocalCounts();
        _setStatus(_modService.HasConfiguredPluginsFolder(settings) ? "Game folder saved." : "Folder saved, but no Plugins folder was found.");
    }

    private async void RefreshMetadata_Click(object sender, RoutedEventArgs e) => await LoadMetadataAsync(true);

    private async Task LoadMetadataAsync(bool forceRefresh = false)
    {
        try
        {
            DebugLogService.Activity("Nexus Dashboard", forceRefresh ? "Refreshing community metadata from the network." : "Loading community metadata.");
            MetadataStatusText.Text = "Requesting current community metadata…";
            var githubTask = forceRefresh && _refreshGitHubData is not null ? _refreshGitHubData() : Task.CompletedTask;
            _allMods = await _metadataService.GetModsAsync(forceRefresh);
            await githubTask;
            var premiumEnabled = _nexusApiKeyStore.HasKey;
            foreach (var mod in _allMods)
            {
                mod.HasPremiumDownload = premiumEnabled;
                mod.RenderedDescription = NexusBbCodeParser.ToDisplayText(mod.Description);
            }
            UpdateLocalModInfo();
            _currentPage = 1;
            ApplyFilters();
            var installedMods = _modService.GetInstalledModsWithMetadata(_settingsService.Load(), _allMods);
            var outdatedCount = installedMods.Count(mod => mod.IsOutOfDate);
            _setStatus(outdatedCount == 0
                ? "Community metadata loaded. Installed mods are up to date."
                : $"Community metadata loaded. {outdatedCount} installed mod(s) are out of date.");
            DebugLogService.Activity("Nexus Dashboard", $"Metadata loaded: {_allMods.Count} mod(s), {outdatedCount} installed mod(s) out of date.");
        }
        catch (Exception exception)
        {
            MetadataStatusText.Text = "Could not load community metadata: " + exception.Message;
            DebugLogService.Error("Community metadata could not be loaded", exception);
            _setStatus("Metadata request failed.");
        }
    }

    private void FiltersChanged(object sender, RoutedEventArgs e)
    {
        _currentPage = 1;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (_allMods.Count == 0)
            return;

        var search = SearchBox.Text.Trim();
        IEnumerable<MetadataMod> filtered = _allMods;
        // The launcher is distributed through GitHub rather than Nexus; never list
        // its informational Nexus entry beside actual game mods.
        filtered = filtered.Where(mod => !mod.Name.Contains("Casualties Hub", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(mod => mod.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                                          || mod.Author.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        if (!AdultContentBox.IsChecked.GetValueOrDefault())
            filtered = filtered.Where(mod => !mod.IsAdultContent);
        if (HideInstalledBox.IsChecked.GetValueOrDefault())
            filtered = filtered.Where(mod => !mod.IsLocallyInstalled);

        var sort = (SortBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Total downloads";
        var descending = !sort.Contains("low", StringComparison.OrdinalIgnoreCase) && !sort.Contains("A-Z", StringComparison.OrdinalIgnoreCase);
        filtered = sort switch
        {
            var value when value.StartsWith("Unique downloads") => descending ? filtered.OrderByDescending(mod => mod.UniqueDownloads) : filtered.OrderBy(mod => mod.UniqueDownloads),
            var value when value.StartsWith("Endorsements") => descending ? filtered.OrderByDescending(mod => mod.Endorsements) : filtered.OrderBy(mod => mod.Endorsements),
            var value when value.StartsWith("Name") => filtered.OrderBy(mod => mod.Name),
            var value when value.StartsWith("Date") => filtered.OrderByDescending(mod => mod.LatestFileModifiedUtc),
            _ => descending ? filtered.OrderByDescending(mod => mod.TotalDownloads) : filtered.OrderBy(mod => mod.TotalDownloads)
        };

        var list = filtered.ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)PageSize));
        _currentPage = Math.Clamp(_currentPage, 1, totalPages);
        MetadataModsList.ItemsSource = list.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
        PageText.Text = $"Page {_currentPage} of {totalPages}";
        MetadataStatusText.Text = $"Showing {list.Count} metadata mods. Displaying {Math.Min(PageSize, list.Count)} per page.";
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage <= 1) return;
        _currentPage--;
        ApplyFilters();
        ScrollToModListTop();
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        var filteredCount = FilteredCount();
        if (_currentPage >= Math.Max(1, (int)Math.Ceiling(filteredCount / (double)PageSize))) return;
        _currentPage++;
        ApplyFilters();
        ScrollToModListTop();
    }

    private void ScrollToModListTop()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var position = MetadataModsList.TransformToAncestor(DashboardScrollViewer).Transform(new Point(0, 0));
            DashboardScrollViewer.ScrollToVerticalOffset(Math.Max(0, DashboardScrollViewer.VerticalOffset + position.Y));
        }));
    }

    private int FilteredCount()
    {
        var search = SearchBox.Text.Trim();
        return _allMods.Count(mod => !mod.Name.Contains("Casualties Hub", StringComparison.OrdinalIgnoreCase)
            && (AdultContentBox.IsChecked.GetValueOrDefault() || !mod.IsAdultContent)
            && (!HideInstalledBox.IsChecked.GetValueOrDefault() || !mod.IsLocallyInstalled)
            && (string.IsNullOrWhiteSpace(search) || mod.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                                || mod.Author.Contains(search, StringComparison.OrdinalIgnoreCase)));
    }

    private async void DownloadOrOpenNexusFiles_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MetadataMod mod })
            return;

        if (mod.IsLocallyDisabled)
        {
            DebugLogService.Activity("Nexus Dashboard", $"Blocked download for disabled mod {mod.Name}.");
            MessageBox.Show("This mod is disabled locally. Re-enable it in Local Mods before downloading or installing an update.", "Mod disabled", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var apiKey = _nexusApiKeyStore.Load();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                DebugLogService.Activity("Nexus Dashboard", $"Starting Premium download for {mod.Name}.");
                if (sender is Button button) button.IsEnabled = false;
                _setStatus($"Downloading {mod.Name} with the Nexus Premium API…");
                var path = await _nexusDownloadService.DownloadLatestFileAsync(mod, apiKey, _settingsService.Load().DownloadPath);
                _setStatus($"Downloaded {Path.GetFileName(path)}. The Hub will offer to install it shortly.");
                return;
            }
            catch (Exception exception)
            {
                DebugLogService.Error($"Premium download failed for {mod.Name}", exception);
                MessageBox.Show(exception.Message, "Nexus Premium download", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (sender is Button button) button.IsEnabled = true;
            }
        }

        var downloadPageUrl = mod.NexusDownloadPageUrl;
        if (string.IsNullOrWhiteSpace(downloadPageUrl))
            return;

        Process.Start(new ProcessStartInfo(downloadPageUrl) { UseShellExecute = true });
        _setStatus($"Opened {mod.Name}'s Nexus files page.");
        DebugLogService.Activity("Nexus Dashboard", $"Opened the Nexus files page for {mod.Name}.");
    }

    private void ViewNexusModPage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MetadataMod mod } || string.IsNullOrWhiteSpace(mod.NexusUrl))
            return;

        Process.Start(new ProcessStartInfo(mod.NexusUrl) { UseShellExecute = true });
        _setStatus($"Opened {mod.Name}'s Nexus mod page.");
        DebugLogService.Activity("Nexus Dashboard", $"Opened the Nexus mod page for {mod.Name}.");
    }

    private void UpdateLocalModInfo()
    {
        var installed = _modService.GetInstalledModsWithMetadata(_settingsService.Load(), _allMods);
        foreach (var mod in _allMods)
        {
            var requirements = DependencyCatalog.GetRequirements([mod.Name]);
            mod.DependenciesLabel = requirements.Count == 0
                ? "No known dependencies"
                : "Requires: " + string.Join(", ", requirements.Select(requirement => requirement.DisplayLabel));
            var local = installed.FirstOrDefault(candidate => string.Equals(candidate.MetadataId, mod.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.Name, mod.Name, StringComparison.OrdinalIgnoreCase));
            mod.IsLocallyInstalled = local is not null;
            mod.IsLocallyDisabled = local?.IsDisabled == true;
            mod.LocalStatusLabel = local is null ? "Not installed" : local.IsOutOfDate ? "Installed — Out of date" : local.IsDisabled ? "Installed — Disabled" : "Installed — Up to date";
        }
    }

    private void ShowModDescription_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: MetadataMod mod } || IsInsideButton(e.OriginalSource as DependencyObject)) return;
        var wasExpanded = mod.IsDescriptionExpanded;
        foreach (var entry in _allMods) entry.IsDescriptionExpanded = false;
        mod.IsDescriptionExpanded = !wasExpanded;
        ApplyFilters();
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

}
