using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Keeps safe copies of plugin files the player does not want a mod install to overwrite.
/// </summary>
public partial class ProtectedFilesPage : UserControl
{
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly ProtectedFilesService _protectedFiles;
    private readonly Action<string> _setStatus;

    public ProtectedFilesPage() : this(_ => { }) { }

    public ProtectedFilesPage(Action<string> setStatus)
    {
        _setStatus = setStatus;
        _protectedFiles = new ProtectedFilesService(_settingsService, _modService);
        AvaloniaXamlLoader.Load(this);

        this.FindControl<Button>("ProtectButton")!.Click += async (_, _) => await ProtectAsync();
        this.FindControl<Button>("RestoreButton")!.Click += async (_, _) => await RestoreAsync();
        this.FindControl<Button>("RefreshButton")!.Click += (_, _) => Reload();
        this.FindControl<Button>("OpenFolderButton")!.Click += OnOpenFolder;

        Reload();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private void Reload()
    {
        List<ProtectedFile> items = [];
        var root = "";
        try
        {
            root = _protectedFiles.GetProtectedRoot();
            items = _protectedFiles.Load();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not read the protected files manifest", exception);
            _setStatus("Could not read protected assets; see the log.");
        }

        this.FindControl<TextBlock>("RootPathText")!.Text =
            string.IsNullOrWhiteSpace(root) ? "No storage folder yet." : root;
        this.FindControl<ItemsControl>("FileList")!.ItemsSource = items;

        var empty = this.FindControl<TextBlock>("EmptyText")!;
        empty.IsVisible = items.Count == 0;
        empty.Text = "Nothing is protected yet. Use \"Protect files...\" to keep a copy of anything you do not want overwritten.";

        this.FindControl<Button>("RestoreButton")!.IsEnabled = items.Count > 0;
        this.FindControl<Button>("OpenFolderButton")!.IsEnabled = Directory.Exists(root);
        this.FindControl<Button>("ProtectButton")!.IsEnabled =
            _modService.HasConfiguredPluginsFolder(_settingsService.Load());

        _setStatus($"{items.Count} protected item(s).");
    }

    private async Task ProtectAsync()
    {
        var owner = Owner;
        if (owner is null) return;

        var settings = _settingsService.Load();
        var plugins = _modService.GetPluginsPath(settings);
        if (!Directory.Exists(plugins))
        {
            await HubDialog.ShowMessageAsync(owner, "No plugins folder",
                "Set your game folder in Settings before protecting anything.");
            return;
        }

        // Start the picker inside the plugins folder: protecting something outside it is
        // meaningless, since only that folder gets overwritten by installs.
        IStorageFolder? start = null;
        try { start = await owner.StorageProvider.TryGetFolderFromPathAsync(plugins); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose files in your plugins folder to protect",
            AllowMultiple = true,
            SuggestedStartLocation = start,
        });

        var paths = files.Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();
        if (paths.Count == 0) return;

        var outside = paths.Where(path => !LinuxPaths.IsInside(path, plugins)).ToList();
        if (outside.Count > 0)
        {
            await HubDialog.ShowMessageAsync(owner, "Those files are outside your plugins folder",
                "Only files inside the BepInEx plugins folder can be protected, because that is the only "
                + "place a mod install writes to.\n\n" + string.Join("\n", outside.Select(path => "  " + path)));
            return;
        }

        try
        {
            _protectedFiles.Protect(settings, paths);
            _setStatus($"Protected {paths.Count} item(s).");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not protect the selected files", exception);
            await HubDialog.ShowMessageAsync(owner, "Could not protect those files", exception.Message);
        }
    }

    private async Task RestoreAsync()
    {
        var owner = Owner;
        if (owner is null) return;

        // Restoring overwrites whatever is currently in the plugins folder under the same names.
        if (!await HubDialog.ConfirmAsync(owner, "Restore protected assets?",
                "Every protected file is copied back into your plugins folder, overwriting anything with the same name.",
                confirm: "Restore", destructive: true))
            return;

        try
        {
            var restored = _protectedFiles.Restore(_settingsService.Load());
            _setStatus($"Restored {restored} protected item(s).");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not restore protected assets", exception);
            await HubDialog.ShowMessageAsync(owner, "Restore failed", exception.Message);
        }
    }

    private async void OnRemove(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ProtectedFile item || Owner is null) return;

        if (!await HubDialog.ConfirmAsync(Owner, "Stop protecting this?",
                $"The saved copy of {item.RelativePath} is deleted. Whatever is currently installed is left alone.",
                confirm: "Remove", destructive: true))
            return;

        try
        {
            _protectedFiles.Remove(item);
            _setStatus($"Removed {item.RelativePath} from protected assets.");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not remove {item.RelativePath}", exception);
            await HubDialog.ShowMessageAsync(Owner, "Could not remove that item", exception.Message);
        }
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        var root = _protectedFiles.GetProtectedRoot();
        if (Directory.Exists(root)) LinuxShell.OpenFolder(root);
    }
}
