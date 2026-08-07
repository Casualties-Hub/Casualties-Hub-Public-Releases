using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Local mod management: list, enable, disable, delete, install from an archive, bulk actions,
/// modlist share codes, and the missing-dependency prompts.
/// </summary>
/// <remarks>
/// The Windows page reads MessageBox results inline, so its handlers are synchronous. Avalonia
/// dialogs are async, so every handler that asks a question is async here and confirm-then-act is
/// awaited rather than inlined.
/// </remarks>
public partial class ModsPage : UserControl
{
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly ModlistService _modlistService;
    private readonly NexusApiKeyStore _apiKeyStore;
    private readonly NexusDownloadService _downloadService = new();
    private readonly Action<string> _setStatus;

    private string _pluginsPath = "";
    private List<InstalledMod> _allMods = [];

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

        this.FindControl<Button>("RefreshButton")!.Click += (_, _) => Reload();
        this.FindControl<Button>("OpenFolderButton")!.Click += OnOpenFolder;
        this.FindControl<Button>("InstallButton")!.Click += OnInstall;
        this.FindControl<Button>("EnableAllButton")!.Click += async (_, _) => await SetAllDisabledAsync(false);
        this.FindControl<Button>("DisableAllButton")!.Click += async (_, _) => await SetAllDisabledAsync(true);
        this.FindControl<Button>("DeleteAllButton")!.Click += async (_, _) => await DeleteAllAsync();
        this.FindControl<Button>("ExportModlistButton")!.Click += async (_, _) => await ExportModlistAsync();
        this.FindControl<Button>("ImportModlistButton")!.Click += async (_, _) => await ImportModlistAsync();

        Reload();
        _ = LoadMetadataAsync();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    /// <summary>
    /// Fetches the community catalogue in the background, then relists so mods pick up their
    /// proper names and update status. The list is shown first from disk alone, so the page is
    /// usable immediately and stays usable if the network is unavailable.
    /// </summary>
    private async Task LoadMetadataAsync()
    {
        if (UniversalMetadataService.LastSuccessfulMods.Count > 0) return;

        try
        {
            var mods = await new UniversalMetadataService(_settingsService).GetModsAsync();
            if (mods.Count > 0) Reload();
        }
        catch (Exception exception)
        {
            // Offline is a normal state for a mod manager; the list already works without this.
            DebugLogService.Info($"Community metadata was not available: {exception.Message}");
        }
    }

    private void Reload()
    {
        var settings = _settingsService.Load();
        _pluginsPath = _modService.GetPluginsPath(settings);

        this.FindControl<TextBlock>("PluginsPathText")!.Text =
            string.IsNullOrWhiteSpace(_pluginsPath) ? "No game folder configured." : _pluginsPath;

        _allMods = [];
        var configured = _modService.HasConfiguredPluginsFolder(settings);
        if (configured)
        {
            try
            {
                // Whatever metadata has already been fetched. Passing an empty list here would
                // leave every mod unmatched, costing names, versions and update status.
                _allMods = _modService.GetInstalledModsWithMetadata(settings, UniversalMetadataService.LastSuccessfulMods);
            }
            catch (Exception exception)
            {
                DebugLogService.Error("Could not read installed mods", exception);
                _setStatus("Could not read the plugins folder; see the log.");
            }
        }

        this.FindControl<ItemsControl>("ModsList")!.ItemsSource = _allMods.Select(mod => new ModRow(mod)).ToList();

        var empty = this.FindControl<TextBlock>("EmptyText")!;
        empty.IsVisible = _allMods.Count == 0;
        empty.Text = !configured
            ? "The BepInEx plugins folder was not found. Install BepInEx into your game folder first, then press Refresh."
            : "No mods are installed yet. Use \"Install from file...\" to add one.";

        foreach (var name in new[] { "InstallButton", "OpenFolderButton", "EnableAllButton", "DisableAllButton", "DeleteAllButton" })
            this.FindControl<Button>(name)!.IsEnabled = configured;

        _setStatus($"{_allMods.Count(mod => !mod.IsDependencyPlaceholder && !mod.IsMissingFromModlist)} mod(s) installed.");
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_pluginsPath)) LinuxShell.OpenFolder(_pluginsPath);
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

    private void OnOpenOutOfDate(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModRow { Mod: var mod }) return;
        var url = mod.NexusUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _setStatus($"{mod.Name} has no Nexus link in the catalogue.");
            return;
        }
        LinuxShell.OpenUrl(url);
        _setStatus($"Opened the Nexus page for {mod.Name}.");
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

    private async void OnIgnoreDependency(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModRow { Mod: { IsDependencyPlaceholder: true } dependency }) return;

        var settings = _settingsService.Load();
        if (!settings.IgnoredDependencyNames.Any(name => DependencyCatalog.NamesMatch(name, dependency.Name)))
            settings.IgnoredDependencyNames.Add(dependency.Name);
        _settingsService.Save(settings);

        Reload();
        _setStatus($"Ignored missing dependency {dependency.Name}.");
        await Task.CompletedTask;
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
            _setStatus($"{action}d {count} plugin file(s).");
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
                + "in Protected Assets or took a backup first. This cannot be undone.",
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

        var exportable = _allMods
            .Where(mod => !mod.IsMissingFromModlist && !mod.IsDisabled && !mod.IsDependencyPlaceholder)
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
        var clipboard = owner.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(code);
            _setStatus($"Copied a share code for {exportable.Count} mod(s) to the clipboard.");
        }
        else
        {
            _setStatus("Share code generated. Copy it from the box above.");
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
            var settings = _settingsService.Load();
            settings.LocalModsShareColumnVisible = true;
            _settingsService.Save(settings);

            Reload();
            var missing = _allMods.Count(mod => mod.IsMissingFromModlist);
            _setStatus($"Imported {entries.Count} entries. {missing} mod(s) you do not have are highlighted.");
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
            var plan = await Task.Run(() =>
                _modService.InspectArchive(settings, path, UniversalMetadataService.LastSuccessfulMods));

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
                           ? $"\n\n{plan.ExistingFilesToReplace.Count} existing file(s) will be replaced."
                           : "")
                       + plan.DependencyPrompt;

            if (!await HubDialog.ConfirmAsync(owner, $"Install {Path.GetFileName(path)}?", body, confirm: "Install"))
                return;

            await Task.Run(() =>
                _modService.InstallArchive(settings, path, UniversalMetadataService.LastSuccessfulMods, skinSlot));
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
