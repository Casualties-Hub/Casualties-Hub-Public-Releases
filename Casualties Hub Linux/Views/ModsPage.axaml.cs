using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Local mod management, laid out as two (optionally three) columns that can each be pointed at
/// any of eight views of the same mod list.
/// </summary>
/// <remarks>
/// The Windows page reads MessageBox results inline, so its handlers are synchronous. Avalonia
/// dialogs are async, so anything that asks a question is async here and confirm-then-act is
/// awaited rather than inlined.
/// </remarks>
public partial class ModsPage : UserControl
{
    /// <summary>The eight column views, in the order the Windows dropdowns list them.</summary>
    private static readonly string[] Views =
    [
        "Enabled Mods",
        "Disabled Mods",
        "Sharecode Requested Mods",
        "Missing Dependencies",
        "Update Available",
        "Incompatibility",
        "Known Bugs",
        "Needs Attention",
    ];

    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly ModlistService _modlistService;
    private readonly NexusApiKeyStore _apiKeyStore;
    private readonly NexusDownloadService _downloadService = new();
    private readonly Action<string> _setStatus;

    private string _pluginsPath = "";
    private List<InstalledMod> _allDisplayMods = [];
    private IReadOnlyList<MetadataMod> _currentMetadata = [];
    private bool _initialised;

    /// <summary>
    /// For Avalonia's XAML loader and the designer, which can only call a parameterless
    /// constructor. The app always uses the overload below so status messages reach the shell.
    /// </summary>
    public ModsPage() : this(_ => { }) { }

    public ModsPage(Action<string> setStatus)
    {
        _setStatus = setStatus;
        _modlistService = new ModlistService(_settingsService);
        _apiKeyStore = new NexusApiKeyStore(_settingsService);
        AvaloniaXamlLoader.Load(this);

        foreach (var (name, defaultIndex) in new[] { ("FirstColumnView", 0), ("SecondColumnView", 1), ("ThirdColumnView", 2) })
        {
            var box = this.FindControl<ComboBox>(name)!;
            box.ItemsSource = Views;
            box.SelectedIndex = defaultIndex;
            box.SelectionChanged += (_, _) => { if (_initialised) ApplyLocalFilter(); };
        }

        this.FindControl<Button>("InstallButton")!.Click += OnInstall;
        this.FindControl<Button>("RefreshButton")!.Click += async (_, _) => await RefreshAsync();
        this.FindControl<Button>("DeleteAllButton")!.Click += async (_, _) => await DeleteAllAsync();
        this.FindControl<Button>("DisableAllButton")!.Click += async (_, _) => await SetAllDisabledAsync(true);
        this.FindControl<Button>("EnableAllButton")!.Click += async (_, _) => await SetAllDisabledAsync(false);
        this.FindControl<Button>("ShareCodeButton")!.Click += async (_, _) => await ExportModlistAsync();
        this.FindControl<Button>("ImportButton")!.Click += async (_, _) => await ImportModlistAsync();
        this.FindControl<Button>("ShareColumnToggleButton")!.Click += (_, _) => ToggleShareColumn();
        this.FindControl<TextBox>("LocalSearchBox")!.TextChanged += (_, _) => { if (_initialised) ApplyLocalFilter(); };

        _initialised = true;
        Reload();
        UpdateShareColumnVisibility();
        _ = RefreshAsync();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    // --- loading -----------------------------------------------------------

    /// <summary>Re-reads the plugins folder, then re-applies the column filters.</summary>
    private void Reload()
    {
        var settings = _settingsService.Load();
        _pluginsPath = _modService.GetPluginsPath(settings);

        var configured = _modService.HasConfiguredPluginsFolder(settings);
        _allDisplayMods = [];

        if (configured)
        {
            try
            {
                _allDisplayMods = _modService.GetInstalledModsWithMetadata(settings, _currentMetadata);
            }
            catch (Exception exception)
            {
                DebugLogService.Error("Could not read installed mods", exception);
                _setStatus("Could not read the plugins folder; see the log.");
            }
        }

        ApplyImportedModlist(_allDisplayMods, _currentMetadata);
        ApplyLocalFilter();

        foreach (var name in new[] { "InstallButton", "DeleteAllButton", "DisableAllButton", "EnableAllButton" })
            this.FindControl<Button>(name)!.IsEnabled = configured;

        var real = _allDisplayMods.Count(mod => !mod.IsMissingFromModlist && !mod.IsDependencyPlaceholder);
        _setStatus(configured
            ? $"{real} mods installed."
            : "No BepInEx plugins folder found. Set your game folder in Settings.");
    }

    /// <summary>Fetches the community catalogue, then relists so names and versions resolve.</summary>
    private async Task RefreshAsync()
    {
        try
        {
            _currentMetadata = await new UniversalMetadataService(_settingsService).GetModsAsync();
        }
        catch (Exception exception)
        {
            // Offline is a normal state for a mod manager; the list still works from disk alone.
            DebugLogService.Info($"Community metadata was not available: {exception.Message}");
            _currentMetadata = UniversalMetadataService.LastSuccessfulMods;
        }

        Reload();
    }

    // --- column filtering --------------------------------------------------

    private void ApplyLocalFilter()
    {
        var search = this.FindControl<TextBox>("LocalSearchBox")!.Text?.Trim() ?? "";

        var filtered = string.IsNullOrWhiteSpace(search)
            ? _allDisplayMods
            : _allDisplayMods.Where(mod =>
                mod.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (mod.ModGuid?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || mod.RequiredDependencies.Any(dependency =>
                    dependency.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        ApplyColumn("FirstColumnView", "FirstModsList", "FirstColumnEmpty", filtered);
        ApplyColumn("SecondColumnView", "SecondModsList", "SecondColumnEmpty", filtered);
        ApplyColumn("ThirdColumnView", "ThirdModsList", "ThirdColumnEmpty", filtered);
    }

    private void ApplyColumn(string viewBoxName, string listName, string emptyTextName, IEnumerable<InstalledMod> source)
    {
        var view = this.FindControl<ComboBox>(viewBoxName)!.SelectedItem as string ?? Views[0];

        var results = (view switch
        {
            "Enabled Mods" => source.Where(mod => !mod.IsMissingFromModlist && !mod.IsDependencyPlaceholder && !mod.IsDisabled),
            "Disabled Mods" => source.Where(mod => !mod.IsMissingFromModlist && !mod.IsDependencyPlaceholder && mod.IsDisabled),
            "Sharecode Requested Mods" => source.Where(mod => mod.IsMissingFromModlist),
            "Missing Dependencies" => CreateMissingDependencyCards(source),
            "Update Available" => source.Where(mod => !mod.IsDisabled && mod.IsOutOfDate),
            "Incompatibility" => source.Where(mod => !mod.IsDisabled && mod.HasIncompatibilities),
            "Known Bugs" => source.Where(mod => !mod.IsDisabled && mod.HasKnownBugs),
            "Needs Attention" => source.Where(mod => !mod.IsDisabled
                && (mod.HasMissingDependencies || mod.IsOutOfDate || mod.HasIncompatibilities || mod.HasKnownBugs)),
            _ => [],
        }).ToList();

        this.FindControl<ListBox>(listName)!.ItemsSource = results.Select(mod => new ModRow(mod)).ToList();

        var empty = this.FindControl<TextBlock>(emptyTextName)!;
        empty.Text = results.Count == 0 ? $"No {view.ToLowerInvariant()} to show." : "";
        empty.IsVisible = results.Count == 0;
    }

    /// <summary>
    /// Builds a card per missing dependency rather than per mod that wants it, so a library three
    /// mods need appears once, listing all three.
    /// </summary>
    private IEnumerable<InstalledMod> CreateMissingDependencyCards(IEnumerable<InstalledMod> source) =>
        source.Where(mod => !mod.IsDisabled)
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
                    DependencyActionLabel = _apiKeyStore.HasKey && metadata is not null ? "Download" : "Open Download",
                    DependencyRequiredByLabel = "Required by: " + requiredBy,
                    UpdateStatusLabel = "Missing dependency",
                };
            });

    /// <summary>
    /// Adds a card for every mod an imported share code asks for that is not installed. Without
    /// this an imported code produces no visible result at all.
    /// </summary>
    private void ApplyImportedModlist(List<InstalledMod> mods, IReadOnlyList<MetadataMod> metadata)
    {
        IReadOnlyList<ModlistEntry> imported;
        try { imported = _modlistService.LoadImported(); }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not read the imported modlist", exception);
            return;
        }

        foreach (var installed in mods)
            installed.IsRequestedByModlist = imported.Any(entry => ModlistEntryMatches(installed, entry));

        foreach (var requested in imported.Where(entry => !mods.Any(installed => ModlistEntryMatches(installed, entry))))
        {
            // Older share codes may carry only a DLL name or GUID, so match those against
            // metadata too and open the exact Nexus files page when one is found.
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
                NexusUrl = metadataMod?.NexusDownloadPageUrl
                           ?? BuildNexusSearchUrl(string.IsNullOrWhiteSpace(requested.Guid) ? requested.Name : requested.Guid),
                ShareCodeActionLabel = string.IsNullOrWhiteSpace(metadataMod?.NexusDownloadPageUrl) ? "Search Nexus" : "Open Download",
                UpdateStatusLabel = "Requested by imported modlist.",
                IsRequestedByModlist = true,
                IsMissingFromModlist = true,
                RequiredDependencies = requirements,
                MissingDependencies = requirements
                    .Where(dependency => !mods.Any(mod => DependencyCatalog.NamesMatch(mod.Name, dependency.Name)))
                    .ToList(),
            });
        }
    }

    private static bool ModlistEntryMatches(InstalledMod installed, ModlistEntry entry) =>
        (!string.IsNullOrWhiteSpace(entry.Id) && entry.Id.Equals(installed.MetadataId, StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(entry.Guid) && entry.Guid.Equals(installed.ModGuid, StringComparison.OrdinalIgnoreCase))
        || entry.Name.Equals(installed.Name, StringComparison.OrdinalIgnoreCase);

    private static MetadataMod? FindMetadataForShareCode(IReadOnlyList<MetadataMod> metadata, ModlistEntry entry) =>
        metadata.FirstOrDefault(mod => !string.IsNullOrWhiteSpace(entry.Id) && mod.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase))
        ?? metadata.FirstOrDefault(mod => !string.IsNullOrWhiteSpace(entry.Guid)
            && (entry.Guid.Equals(mod.PluginGuid, StringComparison.OrdinalIgnoreCase)
                || mod.DllNames.Any(dll => dll.Equals(entry.Guid, StringComparison.OrdinalIgnoreCase))))
        ?? metadata.FirstOrDefault(mod => mod.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase));

    private static string BuildNexusSearchUrl(string term) =>
        "https://www.nexusmods.com/games/casualtiesunknown/mods?keyword=" + Uri.EscapeDataString(term);

    private void ToggleShareColumn()
    {
        var settings = _settingsService.Load();
        settings.LocalModsShareColumnVisible = !settings.LocalModsShareColumnVisible;
        _settingsService.Save(settings);
        UpdateShareColumnVisibility();
    }

    private void UpdateShareColumnVisibility()
    {
        // The user controls this column explicitly. Importing a share code turns it on, but it
        // can always be hidden again without losing the requested entries.
        var show = _settingsService.Load().LocalModsShareColumnVisible;

        this.FindControl<Grid>("ColumnsGrid")!.ColumnDefinitions[2].Width =
            show ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        this.FindControl<Border>("ShareColumnHost")!.IsVisible = show;

        // A bare chevron gave no clue what the button did, so it is labelled.
        var toggle = this.FindControl<Button>("ShareColumnToggleButton")!;
        toggle.Content = show ? "< Hide column" : "Add column >";
        ToolTip.SetTip(toggle, show
            ? "Hide the third Local Mods column."
            : "Show a third Local Mods column, which you can point at any view.");
    }

    // --- single mod actions ------------------------------------------------

    private async void OnToggle(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModRow { Mod: var mod }) return;

        try
        {
            _modService.ToggleModDisabled(mod);
            _setStatus(mod.IsDisabled ? $"Enabled {mod.Name}." : $"Disabled {mod.Name}.");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not toggle {mod.Name}", exception);
            if (Owner is not null)
                await HubDialog.ShowMessageAsync(Owner, "Could not change that mod", exception.Message);
        }
    }

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModRow { Mod: var mod } || Owner is null) return;

        // Deleting is not reversible from inside the Hub, so it always asks first.
        if (!await HubDialog.ConfirmAsync(Owner, $"Delete {mod.Name}?",
                "This permanently removes the mod's files from your plugins folder. It cannot be undone from the Hub.",
                confirm: "Delete", destructive: true))
            return;

        try
        {
            _modService.DeleteInstalledMod(mod);
            _setStatus($"Deleted {mod.Name}.");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not delete {mod.Name}", exception);
            await HubDialog.ShowMessageAsync(Owner, "Could not delete that mod", exception.Message);
        }
    }

    private void OnOpenOutOfDate(object? sender, RoutedEventArgs e) => OpenModLink(sender, "update page");

    private void OnInstallRequested(object? sender, RoutedEventArgs e) => OpenModLink(sender, "download page");

    private void OpenModLink(object? sender, string what)
    {
        if ((sender as Button)?.Tag is not ModRow { Mod: var mod }) return;

        if (string.IsNullOrWhiteSpace(mod.NexusUrl))
        {
            _setStatus($"{mod.Name} has no Nexus link in the catalogue.");
            return;
        }

        LinuxShell.OpenUrl(mod.NexusUrl);
        _setStatus($"Opened the {what} for {mod.Name}.");
    }

    private async void OnIgnoreModlistEntry(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModRow { Mod: { IsMissingFromModlist: true } requested }) return;

        try
        {
            _modlistService.Ignore(new ModlistEntry
            {
                Id = requested.MetadataId ?? "",
                Guid = requested.ModGuid ?? "",
                Name = requested.Name,
                Version = requested.ExpectedVersion ?? "",
            });
            _setStatus($"Removed {requested.Name} from the imported share code.");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not ignore {requested.Name}", exception);
            if (Owner is not null)
                await HubDialog.ShowMessageAsync(Owner, "Could not ignore that entry", exception.Message);
        }
    }

    // --- dependency prompts ------------------------------------------------

    private async void OnDownloadDependency(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModRow { Mod: { IsDependencyPlaceholder: true } dependency }) return;
        var owner = Owner;
        if (owner is null) return;

        var apiKey = _apiKeyStore.Load();
        try
        {
            // With a Premium key the file comes straight down; otherwise open the Nexus page,
            // which is the same rule the dashboard applies.
            if (dependency.DependencyMetadata is { } metadata && !string.IsNullOrWhiteSpace(apiKey))
            {
                if (sender is Button button) button.IsEnabled = false;
                var path = await _downloadService.DownloadLatestFileAsync(
                    metadata, apiKey, _settingsService.Load().DownloadPath);
                _setStatus($"Downloaded {Path.GetFileName(path)}. The Hub will offer to install it shortly.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(dependency.NexusUrl))
            {
                LinuxShell.OpenUrl(dependency.NexusUrl);
                _setStatus($"Opened the Nexus page for {dependency.Name}.");
                return;
            }

            await HubDialog.ShowMessageAsync(owner, "No download link",
                $"The catalogue has no Nexus link for {dependency.Name}.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not fetch dependency {dependency.Name}", exception);
            await HubDialog.ShowMessageAsync(owner, $"Could not download {dependency.Name}", exception.Message);
        }
    }

    private void OnIgnoreDependency(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModRow { Mod: { IsDependencyPlaceholder: true } dependency }) return;

        var settings = _settingsService.Load();
        if (!settings.IgnoredDependencyNames.Any(name => DependencyCatalog.NamesMatch(name, dependency.Name)))
            settings.IgnoredDependencyNames.Add(dependency.Name);
        _settingsService.Save(settings);

        Reload();
        _setStatus($"Ignored missing dependency {dependency.Name}.");
    }

    // --- bulk actions ------------------------------------------------------

    private async Task SetAllDisabledAsync(bool disabled)
    {
        var owner = Owner;
        if (owner is null) return;

        var action = disabled ? "Disable" : "Enable";
        if (!await HubDialog.ConfirmAsync(owner, $"{action} every mod?",
                $"{action}s every plugin DLL in your BepInEx plugins folder.",
                confirm: action, destructive: disabled))
            return;

        try
        {
            var count = _modService.SetAllModsDisabled(_settingsService.Load(), disabled);
            _setStatus($"{action}d {count} plugin files.");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not {action.ToLowerInvariant()} all mods", exception);
            await HubDialog.ShowMessageAsync(owner, $"Could not {action.ToLowerInvariant()} every mod", exception.Message);
        }
    }

    private async Task DeleteAllAsync()
    {
        var owner = Owner;
        if (owner is null) return;

        // The most destructive action in the app: it clears the whole plugins folder, including
        // anything the player put there by hand.
        if (!await HubDialog.ConfirmAsync(owner, "Delete every mod?",
                "This permanently deletes every file and folder inside BepInEx/plugins.\n\n"
                + "Custom pixel art, sprites and other personal files go too, unless you saved them "
                + "in Protected Assets or took a backup first.",
                confirm: "Delete everything", destructive: true))
            return;

        // Asked twice on purpose. A single mis-click should not be able to do this.
        if (!await HubDialog.ConfirmAsync(owner, "Really delete every mod?",
                $"Last chance. Everything in {_pluginsPath} will be removed.",
                confirm: "Yes, delete everything", destructive: true))
            return;

        try
        {
            var backup = _modService.PurgeToBackup(_settingsService.Load());
            _setStatus($"Removed all mods. A copy was kept at {backup}.");
            await HubDialog.ShowMessageAsync(owner, "All mods removed",
                $"Everything was moved to a backup first, so it is recoverable from:\n\n{backup}");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not delete all mods", exception);
            await HubDialog.ShowMessageAsync(owner, "Could not delete every mod", exception.Message);
        }
    }

    // --- modlist share codes -----------------------------------------------

    private async Task ExportModlistAsync()
    {
        var owner = Owner;
        if (owner is null) return;

        var exportable = _allDisplayMods
            .Where(mod => !mod.IsMissingFromModlist && !mod.IsDependencyPlaceholder && !mod.IsDisabled)
            .ToList();

        if (exportable.Count == 0)
        {
            await HubDialog.ShowMessageAsync(owner, "Nothing to share",
                "There are no enabled, recognised mods to put in a share code.");
            return;
        }

        var code = _modlistService.CreateShareCode(exportable);
        this.FindControl<TextBox>("ModlistCodeBox")!.Text = code;

        // Avalonia's clipboard is async and hangs off the TopLevel, unlike WPF's static Clipboard.
        if (owner.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(code);
            _setStatus($"Copied a share code for {exportable.Count} mods to the clipboard.");
        }
        else
        {
            _setStatus("Share code generated. Copy it from the box.");
        }
    }

    private async Task ImportModlistAsync()
    {
        var owner = Owner;
        if (owner is null) return;

        var box = this.FindControl<TextBox>("ModlistCodeBox")!;

        // Fall back to the clipboard when the box is empty, matching the Windows "paste and
        // import in one press" behaviour.
        if (string.IsNullOrWhiteSpace(box.Text) && owner.Clipboard is { } clipboard)
        {
            try { box.Text = await clipboard.TryGetTextAsync(); }
            catch (Exception exception) { DebugLogService.Info($"Clipboard read failed: {exception.Message}"); }
        }

        if (string.IsNullOrWhiteSpace(box.Text))
        {
            await HubDialog.ShowMessageAsync(owner, "No code to import", "Paste a modlist share code first.");
            return;
        }

        try
        {
            var entries = _modlistService.Import(box.Text);

            // Importing reveals the third column, which is where the requested mods land.
            var settings = _settingsService.Load();
            settings.LocalModsShareColumnVisible = true;
            _settingsService.Save(settings);
            UpdateShareColumnVisibility();

            Reload();
            var missing = _allDisplayMods.Count(mod => mod.IsMissingFromModlist);
            _setStatus($"Imported {entries.Count} entries. {missing} mods you do not have are shown in purple.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not import that modlist code", exception);
            await HubDialog.ShowMessageAsync(owner, "That code could not be read", exception.Message);
        }
    }

    // --- install -----------------------------------------------------------

    private async void OnInstall(object? sender, RoutedEventArgs e)
    {
        var owner = Owner;
        if (owner is null) return;

        // StorageProvider rather than a file-dialog class: Avalonia's picker is async and returns
        // storage items, so there is no synchronous OpenFileDialog equivalent to port.
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a mod archive",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Mod archives") { Patterns = ["*.zip", "*.7z", "*.rar"] },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ],
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!ModService.IsSupportedArchive(path))
        {
            await HubDialog.ShowMessageAsync(owner, "Unsupported file", "Choose a .zip, .7z or .rar archive.");
            return;
        }

        var settings = _settingsService.Load();

        try
        {
            // Inspect first: extraction to staging tells us what the archive actually is before
            // anything is written into the game folder.
            var plan = await Task.Run(() => _modService.InspectArchive(settings, path, _currentMetadata));

            if (plan.Kind == ArchiveInstallKind.Unsupported)
            {
                await HubDialog.ShowMessageAsync(owner, "Nothing installable found", plan.Description);
                return;
            }

            string? skinSlot = null;
            if (plan.RequiresSkinSlot)
            {
                // A skin has to be told which character it replaces; there is no safe default,
                // since guessing would silently overwrite somebody else's sprites.
                var picker = new SkinSlotDialog();
                await picker.ShowDialog(owner);
                if (!picker.Confirmed) return;

                skinSlot = picker.SelectedSlot;

                if (picker.SelectedSlotIsOccupied
                    && !await HubDialog.ConfirmAsync(owner,
                        $"Replace the sprites in {skinSlot}?",
                        $"{skinSlot} already contains a skin. Installing over it permanently deletes those sprites.",
                        confirm: "Replace", destructive: true))
                    return;
            }

            var body = plan.Description
                       + (skinSlot is not null ? $"\n\nTarget slot: {skinSlot}" : "")
                       + (plan.ExistingFilesToReplace.Count > 0
                           ? $"\n\n{plan.ExistingFilesToReplace.Count} existing files will be replaced."
                           : "")
                       + plan.DependencyPrompt;

            if (!await HubDialog.ConfirmAsync(owner, $"Install {Path.GetFileName(path)}?", body, confirm: "Install"))
                return;

            await Task.Run(() => _modService.InstallArchive(settings, path, _currentMetadata, skinSlot));
            _setStatus($"Installed {Path.GetFileName(path)}.");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not install {Path.GetFileName(path)}", exception);
            await HubDialog.ShowMessageAsync(owner, "Install failed", exception.Message);
        }
    }
}
