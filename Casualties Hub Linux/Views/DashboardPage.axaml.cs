using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Browses the community metadata catalogue, cross-referenced against what is installed locally.
/// </summary>
/// <remarks>
/// Remote data is untrusted: every field lands in a TextBlock, and links go through
/// <see cref="LinuxShell"/>, which allow-lists the URL scheme. Direct download stays restricted to
/// accounts with a Nexus Premium key; everyone else is sent to the mod's files page in a browser,
/// which is the same rule the Windows Hub applies.
/// </remarks>
public partial class DashboardPage : UserControl
{
    private const int PageSize = 50;

    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly UniversalMetadataService _metadataService;
    private readonly NexusApiKeyStore _apiKeyStore;
    private readonly NexusDownloadService _downloadService = new();
    private readonly GameInstallDetector _detector = new();
    private readonly Action<string> _setStatus;
    private readonly Action? _openSettings;

    private IReadOnlyList<MetadataMod> _allMods = [];
    private List<MetadataMod> _filtered = [];
    private int _currentPage = 1;
    private bool _initialised;
    private DashboardCard? _expandedCard;

    public DashboardPage() : this(_ => { }) { }

    public DashboardPage(Action<string> setStatus, Action? openSettings = null)
    {
        _setStatus = setStatus;
        _openSettings = openSettings;
        _metadataService = new UniversalMetadataService(_settingsService);
        _apiKeyStore = new NexusApiKeyStore(_settingsService);
        AvaloniaXamlLoader.Load(this);

        var filterBox = this.FindControl<ComboBox>("FilterBox")!;
        filterBox.ItemsSource = new[] { "All mods", "Installed", "Not installed", "Updates available" };
        filterBox.SelectedIndex = 0;

        var sortBox = this.FindControl<ComboBox>("SortBox")!;
        sortBox.ItemsSource = new[] { "Most downloaded", "Most endorsed", "Name A-Z", "Author A-Z" };
        sortBox.SelectedIndex = 0;

        _initialised = true;

        filterBox.SelectionChanged += (_, _) => ApplyFilters();
        sortBox.SelectionChanged += (_, _) => ApplyFilters();
        // Adult content is hidden unless explicitly asked for, matching the Windows default.
        this.FindControl<CheckBox>("AdultContentBox")!.IsCheckedChanged += (_, _) => { _currentPage = 1; ApplyFilters(); };
        this.FindControl<TextBox>("SearchBox")!.TextChanged += (_, _) => { _currentPage = 1; ApplyFilters(); };
        this.FindControl<Button>("RefreshButton")!.Click += async (_, _) => await LoadAsync(force: true);
        this.FindControl<Button>("PrevButton")!.Click += (_, _) => ChangePage(-1);
        this.FindControl<Button>("NextButton")!.Click += (_, _) => ChangePage(1);
        this.FindControl<Button>("DetectButton")!.Click += async (_, _) => await DetectAsync();
        this.FindControl<Button>("ChooseFolderButton")!.Click += async (_, _) => await ChooseFolderAsync();
        this.FindControl<Button>("OpenSettingsButton")!.Click += (_, _) => _openSettings?.Invoke();

        // Installing or toggling a mod anywhere in the Hub should be reflected here without the
        // user having to press Refresh.
        ModService.PluginFilesChanged += OnPluginFilesChanged;
        DetachedFromVisualTree += (_, _) => ModService.PluginFilesChanged -= OnPluginFilesChanged;

        RefreshLocalCounts();
        _ = LoadAsync(force: false);
    }

    private void OnPluginFilesChanged(object? sender, EventArgs e)
    {
        // Raised from whichever thread did the file work, so hop to the UI thread first.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnPluginFilesChanged(sender, e));
            return;
        }

        RefreshLocalCounts();
        if (_allMods.Count > 0)
        {
            MarkLocalStatus();
            ApplyFilters();
        }
        _setStatus("Local mod files changed. Dashboard refreshed.");
    }

    private async Task ChooseFolderAsync()
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

        var settings = _settingsService.Load();
        settings.GamePath = path;
        _settingsService.Save(settings);

        RefreshLocalCounts();
        MarkLocalStatus();
        ApplyFilters();
        _setStatus("Game folder set.");
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    /// <summary>Updates the installed count, the game path, and the "no folder set" prompt.</summary>
    private void RefreshLocalCounts()
    {
        var settings = _settingsService.Load();
        var configured = _modService.HasConfiguredGameFolder(settings);

        this.FindControl<Border>("GameFolderCard")!.IsVisible = !configured;
        this.FindControl<TextBlock>("GamePathText")!.Text =
            string.IsNullOrWhiteSpace(settings.GamePath) ? "Not configured" : settings.GamePath;

        var count = 0;
        try { if (configured) count = _modService.GetInstalledMods(settings).Count; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DebugLogService.Info($"Could not count installed mods: {exception.Message}");
        }

        this.FindControl<TextBlock>("ModCountText")!.Text = count.ToString();
    }

    private async Task DetectAsync()
    {
        _setStatus("Scanning Steam libraries...");
        var found = await _detector.FindGameInstallAsync(TimeSpan.FromSeconds(20));
        if (string.IsNullOrWhiteSpace(found))
        {
            _setStatus("No install found. Set the folder in Settings.");
            return;
        }

        var settings = _settingsService.Load();
        settings.GamePath = found;
        _settingsService.Save(settings);
        RefreshLocalCounts();
        MarkLocalStatus();
        ApplyFilters();
        _setStatus("Game folder detected.");
    }

    private async Task LoadAsync(bool force)
    {
        _setStatus(force ? "Refreshing the mod list..." : "Loading the mod list...");
        try
        {
            _allMods = await _metadataService.GetModsAsync(force);
            MarkLocalStatus();
            _currentPage = 1;
            ApplyFilters();
            _setStatus($"{_allMods.Count} mod(s) in the community catalogue.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not load the community metadata", exception);
            _setStatus("Could not reach the mod catalogue. Check your connection.");
            ShowEmpty("The community mod list could not be loaded. The Hub still manages the mods you already have.");
        }
    }

    /// <summary>Cross-references the catalogue against the plugins folder.</summary>
    private void MarkLocalStatus()
    {
        var settings = _settingsService.Load();
        var hasPremiumKey = _apiKeyStore.HasKey;

        List<InstalledMod> installed = [];
        try
        {
            if (_modService.HasConfiguredPluginsFolder(settings))
                installed = _modService.GetInstalledModsWithMetadata(settings, _allMods);
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not read installed mods for the dashboard", exception);
        }

        foreach (var mod in _allMods)
        {
            var match = installed.FirstOrDefault(local =>
                string.Equals(local.MetadataId, mod.Id, StringComparison.Ordinal));

            mod.HasPremiumDownload = hasPremiumKey;
            mod.IsLocallyInstalled = match is not null;
            mod.IsLocallyDisabled = match?.IsDisabled ?? false;
            mod.IsLocallyOutOfDate = match?.IsOutOfDate ?? false;
            // BBCode from Nexus is stripped to plain text before display; no markup reaches the UI.
            mod.RenderedDescription = NexusBbCodeParser.ToDisplayText(mod.Description);

            mod.LocalStatusLabel = match is null
                ? "Not installed"
                : match.IsDisabled
                    ? "Installed, currently disabled"
                    : match.IsOutOfDate
                        ? $"Installed {match.InstalledVersion} — update available"
                        : $"Installed {match.InstalledVersion ?? mod.Version}";

            var requirements = DependencyCatalog.GetRequirements([mod.Name]);
            mod.DependenciesLabel = requirements.Count == 0
                ? "No known dependencies"
                : "Requires: " + string.Join(", ", requirements.Select(requirement => requirement.DisplayLabel));
        }
    }

    private void ApplyFilters()
    {
        if (!_initialised) return;

        var search = this.FindControl<TextBox>("SearchBox")!.Text?.Trim() ?? "";
        var filterIndex = Math.Max(this.FindControl<ComboBox>("FilterBox")!.SelectedIndex, 0);
        var sortIndex = Math.Max(this.FindControl<ComboBox>("SortBox")!.SelectedIndex, 0);

        IEnumerable<MetadataMod> visible = _allMods;

        if (this.FindControl<CheckBox>("AdultContentBox")!.IsChecked != true)
            visible = visible.Where(mod => !mod.IsAdultContent);

        visible = filterIndex switch
        {
            1 => visible.Where(mod => mod.IsLocallyInstalled),
            2 => visible.Where(mod => !mod.IsLocallyInstalled),
            3 => visible.Where(mod => mod.IsLocallyOutOfDate),
            _ => visible,
        };

        if (search.Length > 0)
            visible = visible.Where(mod =>
                mod.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || mod.Author.Contains(search, StringComparison.OrdinalIgnoreCase)
                || mod.RenderedDescription.Contains(search, StringComparison.OrdinalIgnoreCase));

        visible = sortIndex switch
        {
            1 => visible.OrderByDescending(mod => mod.Endorsements),
            2 => visible.OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase),
            3 => visible.OrderBy(mod => mod.Author, StringComparer.OrdinalIgnoreCase),
            _ => visible.OrderByDescending(mod => mod.TotalDownloads),
        };

        _filtered = visible.ToList();
        ShowPage();
    }

    private void ShowPage()
    {
        var pageCount = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
        _currentPage = Math.Clamp(_currentPage, 1, pageCount);

        var rows = _filtered.Skip((_currentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(mod => new DashboardCard(mod))
            .ToList();

        _expandedCard = null;
        this.FindControl<ItemsControl>("ModList")!.ItemsSource = rows;

        // Icons load after the cards are on screen, so the page never waits on the network.
        foreach (var card in rows) _ = card.LoadIconAsync();

        this.FindControl<TextBlock>("PageText")!.Text = $"Page {_currentPage} of {pageCount}   ·   {_filtered.Count} mod(s)";
        this.FindControl<Button>("PrevButton")!.IsEnabled = _currentPage > 1;
        this.FindControl<Button>("NextButton")!.IsEnabled = _currentPage < pageCount;

        this.FindControl<TextBlock>("SummaryText")!.Text =
            $"{_allMods.Count} in the catalogue · {_allMods.Count(mod => mod.IsLocallyInstalled)} installed · "
            + $"{_allMods.Count(mod => mod.IsLocallyOutOfDate)} out of date";

        var empty = this.FindControl<TextBlock>("EmptyText")!;
        if (_filtered.Count == 0 && _allMods.Count > 0)
        {
            empty.IsVisible = true;
            empty.Text = "No mods match that search or filter.";
        }
        else
        {
            empty.IsVisible = false;
        }

        // A new page should start at the top rather than keeping the previous scroll position.
        Dispatcher.UIThread.Post(() => this.FindControl<ScrollViewer>("ModScroller")!.Offset = default);
    }

    private void ShowEmpty(string message)
    {
        this.FindControl<ItemsControl>("ModList")!.ItemsSource = Array.Empty<MetadataMod>();
        var empty = this.FindControl<TextBlock>("EmptyText")!;
        empty.IsVisible = true;
        empty.Text = message;
    }

    private void ChangePage(int delta)
    {
        _currentPage += delta;
        ShowPage();
    }

    /// <summary>Clicking a card flips its description overlay, one at a time.</summary>
    private void OnCardPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if ((sender as Control)?.Tag is not DashboardCard card) return;

        // Buttons inside the card raise this too; ignore the press when it landed on one, or
        // Download would also toggle the description over the top of itself.
        if (e.Source is Control source && source.FindAncestorOfType<Button>(includeSelf: true) is not null) return;

        var wasExpanded = card.IsDescriptionExpanded;
        if (_expandedCard is not null) _expandedCard.IsDescriptionExpanded = false;
        card.IsDescriptionExpanded = !wasExpanded;
        _expandedCard = card.IsDescriptionExpanded ? card : null;
    }

    private void OnOpenNexus(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not DashboardCard { Mod: var mod }) return;
        if (string.IsNullOrWhiteSpace(mod.NexusUrl))
        {
            _setStatus($"{mod.Name} has no Nexus link in the catalogue.");
            return;
        }
        LinuxShell.OpenUrl(mod.NexusUrl);
    }

    private async void OnAction(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not DashboardCard { Mod: var mod } || Owner is null) return;

        if (mod.IsLocallyDisabled)
        {
            await HubDialog.ShowMessageAsync(Owner, $"{mod.Name} is disabled",
                "This mod is installed but switched off. Re-enable it from the Local Mods page.");
            return;
        }

        // No Premium key: hand off to the browser rather than pretending we can fetch it.
        if (!mod.HasPremiumDownload)
        {
            var page = mod.NexusDownloadPageUrl ?? mod.NexusUrl;
            if (string.IsNullOrWhiteSpace(page))
            {
                await HubDialog.ShowMessageAsync(Owner, "No download link",
                    $"The catalogue has no Nexus link for {mod.Name}.");
                return;
            }

            LinuxShell.OpenUrl(page);
            _setStatus($"Opened the Nexus files page for {mod.Name}. The Hub will offer to install it once it finishes downloading.");
            return;
        }

        await DownloadAsync(mod);
    }

    private async Task DownloadAsync(MetadataMod mod)
    {
        var owner = Owner;
        if (owner is null) return;

        var apiKey = _apiKeyStore.Load();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await HubDialog.ShowMessageAsync(owner, "No API key saved",
                "Add your personal Nexus Premium API key in Settings to download directly.");
            return;
        }

        var settings = _settingsService.Load();
        _setStatus($"Downloading {mod.Name}...");

        try
        {
            var path = await _downloadService.DownloadLatestFileAsync(mod, apiKey, settings.DownloadPath);
            // The download folder watcher picks the file up and offers to install it, so this
            // deliberately stops at "downloaded" rather than installing behind the user's back.
            _setStatus($"Downloaded {System.IO.Path.GetFileName(path)}.");
            DebugLogService.Activity("Nexus Dashboard", $"Downloaded {mod.Name} to {path}.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not download {mod.Name}", exception);
            await HubDialog.ShowMessageAsync(owner, $"Could not download {mod.Name}", exception.Message);
            _setStatus("Download failed.");
        }
    }
}
