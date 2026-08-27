using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Create, inspect, restore and remove backups of the plugins folder.
/// </summary>
/// <remarks>
/// This exists because the Hub can permanently delete mods. Taking a backup is purely
/// additive and never touches the originals, so it is safe to offer without warnings; restoring
/// and deleting both ask first, because they are not.
/// </remarks>
public partial class BackupsPage : UserControl
{
    /// <summary>One backup folder, described for the list.</summary>
    private sealed record BackupEntry(string Path, string Title, string Summary);

    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly Action<string> _setStatus;

    public BackupsPage() : this(_ => { }) { }

    public BackupsPage(Action<string> setStatus)
    {
        _setStatus = setStatus;
        AvaloniaXamlLoader.Load(this);

        this.FindControl<Button>("CreateButton")!.Click += async (_, _) => await CreateAsync();
        this.FindControl<Button>("RefreshButton")!.Click += (_, _) => Reload();
        this.FindControl<Button>("OpenFolderButton")!.Click += (_, _) =>
        {
            var root = ModService.BackupRoot(_settingsService.Load());
            if (Directory.Exists(root)) LinuxShell.OpenFolder(root);
            else _setStatus("No backups folder yet. Take a backup first.");
        };

        Reload();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private void Reload()
    {
        var settings = _settingsService.Load();
        var root = ModService.BackupRoot(settings);
        this.FindControl<TextBlock>("BackupRootText")!.Text = root;

        var entries = new List<BackupEntry>();
        try
        {
            if (Directory.Exists(root))
            {
                foreach (var directory in Directory.EnumerateDirectories(root)
                             .OrderByDescending(path => path, StringComparer.Ordinal))
                {
                    var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToList();
                    var bytes = files.Sum(file => new FileInfo(file).Length);
                    entries.Add(new BackupEntry(
                        directory,
                        Path.GetFileName(directory).Replace('_', ' '),
                        $"{files.Count} files · {bytes / 1024.0 / 1024.0:F1} MB"));
                }
            }
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not list backups", exception);
            _setStatus("Could not read the backups folder; see the log.");
        }

        this.FindControl<ItemsControl>("BackupList")!.ItemsSource = entries;

        var empty = this.FindControl<TextBlock>("EmptyText")!;
        empty.IsVisible = entries.Count == 0;
        empty.Text = "No backups yet. \"Back up now\" copies your current plugins folder.";

        var canBackUp = _modService.HasConfiguredPluginsFolder(settings);
        this.FindControl<Button>("CreateButton")!.IsEnabled = canBackUp;
        if (!canBackUp) empty.Text = "Set your game folder in Settings before taking a backup.";
    }

    private async Task CreateAsync()
    {
        _setStatus("Backing up...");
        try
        {
            var settings = _settingsService.Load();
            var path = await Task.Run(() => _modService.BackupPlugins(settings));
            _setStatus($"Backup created: {Path.GetFileName(path)}");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Backup failed", exception);
            _setStatus("Backup failed.");
            if (Owner is not null) await HubDialog.ShowMessageAsync(Owner, "Backup failed", exception.Message);
        }
    }

    private void OnOpenBackup(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is BackupEntry entry && Directory.Exists(entry.Path))
            LinuxShell.OpenFolder(entry.Path);
    }

    private async void OnRestore(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not BackupEntry entry || Owner is null) return;

        // Restoring overwrites same-named files in the live plugins folder, so it is a
        // destructive action even though nothing is deleted outright.
        if (!await HubDialog.ConfirmAsync(Owner,
                $"Restore {entry.Title}?",
                "Files from this backup will be copied into your plugins folder, overwriting anything with the same name. "
                + "Mods added since the backup are left alone.",
                confirm: "Restore", destructive: true))
            return;

        try
        {
            var settings = _settingsService.Load();
            var plugins = _modService.GetPluginsPath(settings);
            if (!Directory.Exists(plugins)) throw new DirectoryNotFoundException("The plugins folder was not found.");

            var restored = await Task.Run(() => ModService.RestoreBackup(entry.Path, plugins));
            _setStatus($"Restored {restored} files from {entry.Title}.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not restore {entry.Title}", exception);
            await HubDialog.ShowMessageAsync(Owner, "Restore failed", exception.Message);
        }
    }

    private async void OnDeleteBackup(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not BackupEntry entry || Owner is null) return;

        if (!await HubDialog.ConfirmAsync(Owner,
                $"Delete backup {entry.Title}?",
                "This removes the backup folder permanently. Your installed mods are not affected.",
                confirm: "Delete", destructive: true))
            return;

        try
        {
            // Guard against a settings file pointing BackupPath somewhere unexpected: never
            // recursively delete anything that is not inside the backups root.
            var root = ModService.BackupRoot(_settingsService.Load());
            if (!LinuxPaths.IsInside(entry.Path, root))
                throw new InvalidOperationException("That folder is not inside the backups directory.");

            Directory.Delete(entry.Path, recursive: true);
            _setStatus($"Deleted backup {entry.Title}.");
            Reload();
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Could not delete backup {entry.Title}", exception);
            await HubDialog.ShowMessageAsync(Owner, "Could not delete that backup", exception.Message);
        }
    }
}
