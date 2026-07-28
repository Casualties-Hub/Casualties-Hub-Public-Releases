using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class ModsPage : Page
{
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly ModlistService _modlistService;
    private readonly UniversalMetadataService _metadataService;
    private readonly NexusDownloadService _nexusDownloadService = new();
    private readonly NexusApiKeyStore _nexusApiKeyStore;
    private readonly Action<string> _setStatus;
    private readonly DispatcherTimer _pluginRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private FileSystemWatcher? _pluginsWatcher;
    private bool _isRefreshing;
    private List<InstalledMod> _allDisplayMods = [];
    private IReadOnlyList<MetadataMod> _currentMetadata = [];

    public ModsPage(Action<string> setStatus)
    {
        InitializeComponent();
        _setStatus = setStatus;
        _metadataService = new(_settingsService);
        _modlistService = new(_settingsService);
        _nexusApiKeyStore = new(_settingsService);
        _pluginRefreshTimer.Tick += PluginRefreshTimer_Tick;
        Loaded += ModsPage_Loaded;
        Unloaded += ModsPage_Unloaded;
    }

    private async void ModsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ModService.PluginFilesChanged += PluginFilesChanged;
        StartPluginWatcher();
        await RefreshAsync();
    }

    private void ModsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ModService.PluginFilesChanged -= PluginFilesChanged;
        StopPluginWatcher();
        _pluginRefreshTimer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
        var settings = _settingsService.Load();
        IReadOnlyList<MetadataMod> metadata = UniversalMetadataService.LastSuccessfulMods;
        try
        {
            if (metadata.Count == 0)
                metadata = await _metadataService.GetModsAsync();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not refresh metadata for update checks", exception);
            _setStatus("Showing installed mods, but update metadata could not be loaded.");
        }

        var mods = _modService.GetInstalledModsWithMetadata(settings, metadata)
            .Where(mod => !string.IsNullOrWhiteSpace(mod.InstalledVersion))
            .ToList();
        ApplyImportedModlist(mods, metadata);
        _allDisplayMods = mods;
        _currentMetadata = metadata;
        ApplyLocalFilter();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void StartPluginWatcher()
    {
        StopPluginWatcher();
        var pluginsPath = _modService.GetPluginsPath(_settingsService.Load());
        if (!Directory.Exists(pluginsPath)) return;

        _pluginsWatcher = new FileSystemWatcher(pluginsPath)
        {
            IncludeSubdirectories = true,
            Filter = "*.*",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };
        _pluginsWatcher.Changed += PluginDirectoryChanged;
        _pluginsWatcher.Created += PluginDirectoryChanged;
        _pluginsWatcher.Deleted += PluginDirectoryChanged;
        _pluginsWatcher.Renamed += PluginDirectoryRenamed;
        _pluginsWatcher.Error += PluginWatcherError;
        _pluginsWatcher.EnableRaisingEvents = true;
    }

    private void StopPluginWatcher()
    {
        if (_pluginsWatcher is null) return;
        _pluginsWatcher.EnableRaisingEvents = false;
        _pluginsWatcher.Changed -= PluginDirectoryChanged;
        _pluginsWatcher.Created -= PluginDirectoryChanged;
        _pluginsWatcher.Deleted -= PluginDirectoryChanged;
        _pluginsWatcher.Renamed -= PluginDirectoryRenamed;
        _pluginsWatcher.Error -= PluginWatcherError;
        _pluginsWatcher.Dispose();
        _pluginsWatcher = null;
    }

    private void PluginFilesChanged(object? sender, EventArgs e) => QueuePluginRefresh();
    private void PluginDirectoryChanged(object sender, FileSystemEventArgs e) => QueuePluginRefresh();
    private void PluginDirectoryRenamed(object sender, RenamedEventArgs e) => QueuePluginRefresh();
    private void PluginWatcherError(object sender, ErrorEventArgs e) => DebugLogService.Error("Local Mods file watcher encountered an error", e.GetException());

    private void QueuePluginRefresh()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(QueuePluginRefresh);
            return;
        }

        _pluginRefreshTimer.Stop();
        _pluginRefreshTimer.Start();
    }

    private async void PluginRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _pluginRefreshTimer.Stop();
        await RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
        DebugLogService.Info("Plugins list refreshed.");
        _setStatus("Plugins list refreshed.");
    }

    private void LocalSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyLocalFilter();

    private void ColumnView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyLocalFilter();
    }

    private void ApplyLocalFilter()
    {
        var search = LocalSearchBox?.Text.Trim() ?? "";
        var filtered = string.IsNullOrWhiteSpace(search)
            ? _allDisplayMods
            : _allDisplayMods.Where(mod => mod.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (mod.ModGuid?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || mod.RequiredDependencies.Any(dependency => dependency.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        ApplyColumn(FirstColumnView, FirstModsList, FirstColumnEmpty, filtered);
        ApplyColumn(SecondColumnView, SecondModsList, SecondColumnEmpty, filtered);
        ApplyColumn(ThirdColumnView, ThirdModsList, ThirdColumnEmpty, filtered);
        UpdateShareColumnVisibility();
    }

    private static string SelectedView(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Enabled Mods";

    private void ApplyColumn(ComboBox viewBox, ListBox listBox, TextBlock emptyText, IEnumerable<InstalledMod> source)
    {
        var view = SelectedView(viewBox);
        var results = (view switch
        {
            "Enabled Mods" => source.Where(mod => !mod.IsMissingFromModlist && !mod.IsDisabled),
            "Disabled Mods" => source.Where(mod => !mod.IsMissingFromModlist && mod.IsDisabled),
            "Sharecode Requested Mods" => source.Where(mod => mod.IsMissingFromModlist),
            "Missing Dependencies" => CreateMissingDependencyCards(source),
            "Update Available" => source.Where(mod => !mod.IsDisabled && mod.IsOutOfDate),
            "Incompatibility" => source.Where(mod => !mod.IsDisabled && mod.HasIncompatibilities),
            "Known Bugs" => source.Where(mod => !mod.IsDisabled && mod.HasKnownBugs),
            "Needs Attention" => source.Where(mod => !mod.IsDisabled && (mod.HasMissingDependencies || mod.IsOutOfDate || mod.HasIncompatibilities || mod.HasKnownBugs)),
            _ => Enumerable.Empty<InstalledMod>()
        }).ToList();
        listBox.ItemsSource = results;
        emptyText.Text = results.Count == 0 ? $"No {view.ToLowerInvariant()} to show." : "";
        emptyText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private IEnumerable<InstalledMod> CreateMissingDependencyCards(IEnumerable<InstalledMod> source)
    {
        return source.Where(mod => !mod.IsDisabled)
            .SelectMany(mod => mod.MissingDependencies.Select(requirement => new { requirement, RequiredBy = mod.Name }))
            .GroupBy(item => item.requirement.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var dependency = group.First().requirement;
                var metadata = _currentMetadata.FirstOrDefault(mod => DependencyCatalog.NamesMatch(mod.Name, dependency.Name));
                var requiredBy = string.Join(", ", group.Select(item => item.RequiredBy).Distinct(StringComparer.OrdinalIgnoreCase));
                return new InstalledMod
                {
                    Name = metadata?.Name ?? dependency.Name,
                    MetadataId = metadata?.Id,
                    NexusUrl = metadata?.NexusDownloadPageUrl ?? BuildNexusSearchUrl(dependency.Name),
                    IsDependencyPlaceholder = true,
                    DependencyMetadata = metadata,
                    DependencyActionLabel = _nexusApiKeyStore.HasKey && metadata is not null ? "Download" : "Open Download",
                    DependencyRequiredByLabel = "Required by: " + requiredBy,
                    UpdateStatusLabel = "Missing dependency"
                };
            });
    }

    private void UpdateShareColumnVisibility()
    {
        var settings = _settingsService.Load();
        // The user controls this column explicitly. Importing a share code turns it on,
        // but it may always be hidden again without losing the requested entries.
        var show = settings.LocalModsShareColumnVisible;
        ShareColumnDefinition.Width = show ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        ShareColumnHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ShareColumnToggleButton.Content = show ? "<" : ">";
        ShareColumnToggleButton.ToolTip = show
            ? "Hide the third Local Mods column."
            : "Show the third Local Mods column.";
    }

    private void ToggleShareColumn_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        settings.LocalModsShareColumnVisible = !settings.LocalModsShareColumnVisible;
        _settingsService.Save(settings);
        UpdateShareColumnVisibility();
    }

    private void ApplyImportedModlist(List<InstalledMod> mods, IReadOnlyList<MetadataMod> metadata)
    {
        var imported = _modlistService.LoadImported();
        foreach (var installed in mods)
            installed.IsRequestedByModlist = imported.Any(entry => ModlistEntryMatches(installed, entry));

        foreach (var requested in imported.Where(entry => !mods.Any(installed => ModlistEntryMatches(installed, entry))))
        {
            // Older share codes may only contain a DLL name or GUID. Match those
            // against metadata DLL names too, so this opens the exact Nexus Files page.
            var metadataMod = FindMetadataForShareCode(metadata, requested);
            var name = metadataMod?.Name ?? requested.Name;
            var requirements = DependencyCatalog.GetRequirements([name]);
            mods.Add(new InstalledMod
            {
                Name = name,
                MetadataId = metadataMod?.Id ?? requested.Id,
                ModGuid = requested.Guid,
                InstalledVersion = "Not installed",
                ExpectedVersion = metadataMod?.Version ?? requested.Version,
                // Metadata with a GUID gives us an exact Nexus files page. Older
                // metadata cannot identify a GUID, so give the player a useful
                // Nexus search instead of a dead purple entry.
                NexusUrl = metadataMod?.NexusDownloadPageUrl ?? BuildNexusSearchUrl(string.IsNullOrWhiteSpace(requested.Guid) ? requested.Name : requested.Guid),
                ShareCodeActionLabel = string.IsNullOrWhiteSpace(metadataMod?.NexusDownloadPageUrl) ? "Search Nexus" : "Open Download",
                UpdateStatusLabel = "Requested by imported modlist.",
                IsRequestedByModlist = true,
                IsMissingFromModlist = true,
                RequiredDependencies = requirements,
                MissingDependencies = requirements.Where(dependency => !mods.Any(mod => DependencyCatalog.NamesMatch(mod.Name, dependency.Name))).ToList()
            });
        }
    }

    private static bool ModlistEntryMatches(InstalledMod installed, ModlistEntry entry) =>
        (!string.IsNullOrWhiteSpace(entry.Id) && entry.Id.Equals(installed.MetadataId, StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(entry.Guid) && entry.Guid.Equals(installed.ModGuid, StringComparison.OrdinalIgnoreCase))
        || entry.Name.Equals(installed.Name, StringComparison.OrdinalIgnoreCase);

    private static MetadataMod? FindMetadataForShareCode(IEnumerable<MetadataMod> metadata, ModlistEntry entry)
    {
        var requestedIdentifiers = new[] { entry.Id, entry.Guid, entry.Name }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeModIdentifier)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        return metadata.FirstOrDefault(mod =>
            requestedIdentifiers.Contains(NormalizeModIdentifier(mod.Id))
            || requestedIdentifiers.Contains(NormalizeModIdentifier(mod.PluginGuid))
            || requestedIdentifiers.Contains(NormalizeModIdentifier(mod.Name))
            || mod.DllNames.Any(dll => requestedIdentifiers.Contains(NormalizeModIdentifier(dll))));
    }

    private static string NormalizeModIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(value.Trim());
        return new string(fileName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string BuildNexusSearchUrl(string guid) =>
        "https://www.nexusmods.com/scavprototype/search/?q=" + Uri.EscapeDataString(guid);

    private void ExportModlist_Click(object sender, RoutedEventArgs e)
    {
        if (_allDisplayMods.Count == 0)
        {
            MessageBox.Show("There are no recognized installed mods to export.", "Modlist Share Code", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var code = _modlistService.CreateShareCode(_allDisplayMods.Where(mod => !mod.IsMissingFromModlist && !mod.IsDisabled));
        ModlistCodeBox.Text = code;
        try
        {
            Clipboard.SetText(code);
            MessageBox.Show("Your Modlist Share Code was copied to the clipboard.", "Modlist Share Code", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not copy modlist code to clipboard", exception);
            MessageBox.Show("The share code was created, but Windows could not copy it to the clipboard. You can still copy it from the box.", "Modlist Share Code", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _setStatus("Modlist share code created and copied to the clipboard.");
    }

    private async void PasteImportModlist_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ModlistCodeBox.Text) && Clipboard.ContainsText())
                ModlistCodeBox.Text = Clipboard.GetText();
            var entries = _modlistService.Import(ModlistCodeBox.Text);
            var settings = _settingsService.Load();
            settings.LocalModsShareColumnVisible = true;
            _settingsService.Save(settings);
            await RefreshAsync();
            var missing = _allDisplayMods.Count(mod => mod.IsMissingFromModlist);
            _setStatus($"Imported {entries.Count} modlist entries. {missing} missing mod(s) are purple.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not import modlist share code", exception);
            MessageBox.Show(exception.Message, "Modlist Share Code", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void InstallArchive_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        if (!_modService.HasConfiguredGameFolder(settings))
        {
            MessageBox.Show("Set a valid game, BepInEx, or Plugins folder first.", "Casualties Hub");
            return;
        }

        var dialog = new OpenFileDialog { Filter = "Mod archives (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar", Title = "Select a mod archive" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var metadata = UniversalMetadataService.LastSuccessfulMods;
            if (metadata.Count == 0) metadata = await _metadataService.GetModsAsync();
            var plan = _modService.InspectArchive(settings, dialog.FileName, metadata);
            if (plan.Kind == ArchiveInstallKind.Unsupported)
            {
                MessageBox.Show(plan.Description, "Unsupported archive layout", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var replacementText = plan.ExistingFilesToReplace.Count == 0
                ? ""
                : $"\n\nThis update/reinstall will replace {plan.ExistingFilesToReplace.Count} existing file(s) that match the new archive. BepInEx itself will not be removed.";
            if (MessageBox.Show($"{plan.Description}{replacementText}{plan.DependencyPrompt}\n\nContinue?", "Install archive", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            string? skinSlot = null;
            if (plan.RequiresSkinSlot)
            {
                var slotDialog = new SkinSlotDialog { Owner = Window.GetWindow(this) };
                if (slotDialog.ShowDialog() != true) return;
                skinSlot = slotDialog.SelectedSlot;
                if (MessageBox.Show($"The current CustomSprites\\{skinSlot} contents will be permanently replaced. Continue?", "Replace sprite slot", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
            }

            _modService.InstallArchive(settings, dialog.FileName, metadata, skinSlot);
            await RefreshAsync();
            DebugLogService.Info($"Installed archive: {dialog.FileName}");
            _setStatus("Archive installed.");
        }
        catch (Exception ex)
        {
            DebugLogService.Error("Archive installation failed", ex);
            MessageBox.Show(ex.Message, "Install failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteAllMods_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        if (!_modService.HasConfiguredPluginsFolder(settings))
        {
            MessageBox.Show("Set a game folder containing BepInEx\\Plugins first.", "Casualties Hub");
            return;
        }

        const string warning = "This permanently deletes every file and folder inside BepInEx\\Plugins.\n\nCustom pixel art, sprites, and other personal files will be deleted unless you save them first in Protected Files. This cannot be undone.\n\nDelete all mods?";
        if (MessageBox.Show(warning, "Delete all mods", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            var deleted = _modService.DeleteAllPluginContents(settings);
            _ = RefreshAsync();
            DebugLogService.Info($"Deleted {deleted} item(s) from BepInEx Plugins.");
            _setStatus($"Deleted {deleted} item(s) from Plugins.");
        }
        catch (Exception ex)
        {
            DebugLogService.Error("Delete all mods failed", ex);
            MessageBox.Show(ex.Message, "Delete all mods failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenOutOfDateMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledMod mod })
            return;

        if (mod.IsDisabled)
        {
            MessageBox.Show("Re-enable this mod in Local Mods before checking for or installing an update.", "Mod disabled", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(mod.NexusUrl))
        {
            Process.Start(new ProcessStartInfo(mod.NexusUrl) { UseShellExecute = true });
            _setStatus($"Opened {mod.Name} on Nexus.");
        }
    }

    private void InstallRequestedMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledMod mod }) return;
        if (string.IsNullOrWhiteSpace(mod.NexusUrl))
        {
            MessageBox.Show("No Nexus page is known for this requested mod yet.", "Install requested mod", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo(mod.NexusUrl) { UseShellExecute = true });
        var destination = mod.NexusUrl.Contains("?tab=files", StringComparison.OrdinalIgnoreCase)
            ? "Nexus download page"
            : "Nexus search page";
        DebugLogService.Activity("Share Code", $"Opened the {destination} for requested mod {mod.Name}.");
        _setStatus($"Opened {destination} for {mod.Name}.");
    }

    private async void ToggleDisable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledMod mod }) return;

        try
        {
            _modService.ToggleModDisabled(mod);
            await RefreshAsync();
            _setStatus(mod.IsDisabled ? $"Enabled {mod.Name}." : $"Disabled {mod.Name}.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not change enabled state for {mod.Name}", exception);
            MessageBox.Show(exception.Message, "Could not change mod state", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DisableAll_Click(object sender, RoutedEventArgs e) => await SetAllModsDisabledAsync(true);
    private async void EnableAll_Click(object sender, RoutedEventArgs e) => await SetAllModsDisabledAsync(false);

    private async Task SetAllModsDisabledAsync(bool disabled)
    {
        var settings = _settingsService.Load();
        if (!_modService.HasConfiguredPluginsFolder(settings))
        {
            MessageBox.Show("Set a game folder containing BepInEx\\Plugins first.", "Local Mods");
            return;
        }
        var action = disabled ? "disable" : "enable";
        if (MessageBox.Show(char.ToUpper(action[0]) + action[1..] + " every DLL in BepInEx\\Plugins?", "Local Mods", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            var changed = _modService.SetAllModsDisabled(settings, disabled);
            await RefreshAsync();
            _setStatus((disabled ? "Disabled" : "Enabled") + " " + changed + " DLL(s).");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not " + action + " all mods", exception);
            MessageBox.Show(exception.Message, "Local Mods", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledMod mod }) return;
        if (MessageBox.Show("Delete '" + mod.Name + "' from BepInEx\\Plugins? This cannot be undone.", "Delete local mod", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            _modService.DeleteInstalledMod(mod);
            await RefreshAsync();
            _setStatus("Deleted " + mod.Name + ".");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not delete " + mod.Name, exception);
            MessageBox.Show(exception.Message, "Delete local mod", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void IgnoreModlistEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledMod mod }) return;
        _modlistService.Ignore(new ModlistEntry { Id = mod.MetadataId ?? "", Name = mod.Name, Guid = mod.ModGuid ?? "" });
        await RefreshAsync();
        _setStatus("Ignored " + mod.Name + " from the imported modlist.");
    }

    private async void DownloadMissingDependency_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledMod { IsDependencyPlaceholder: true } dependency }) return;
        var metadata = dependency.DependencyMetadata;
        var apiKey = _nexusApiKeyStore.Load();
        try
        {
            if (metadata is not null && !string.IsNullOrWhiteSpace(apiKey))
            {
                if (sender is Button button) button.IsEnabled = false;
                var path = await _nexusDownloadService.DownloadLatestFileAsync(metadata, apiKey, _settingsService.Load().DownloadPath);
                _setStatus($"Downloaded {Path.GetFileName(path)}. The Hub will offer to install it shortly.");
                return;
            }
            if (!string.IsNullOrWhiteSpace(dependency.NexusUrl))
            {
                Process.Start(new ProcessStartInfo(dependency.NexusUrl) { UseShellExecute = true });
                _setStatus($"Opened the Nexus download page for {dependency.Name}.");
            }
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not download missing dependency {dependency.Name}", exception);
            MessageBox.Show(exception.Message, "Missing dependency", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (sender is Button button) button.IsEnabled = true;
        }
    }

    private async void IgnoreMissingDependency_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: InstalledMod { IsDependencyPlaceholder: true } dependency }) return;
        var settings = _settingsService.Load();
        if (!settings.IgnoredDependencyNames.Any(name => DependencyCatalog.NamesMatch(name, dependency.Name)))
            settings.IgnoredDependencyNames.Add(dependency.Name);
        _settingsService.Save(settings);
        await RefreshAsync();
        _setStatus($"Ignored missing dependency {dependency.Name}.");
    }

}
