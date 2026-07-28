using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class ProtectedFilesPage : Page
{
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly ProtectedFilesService _protectedService;
    private readonly Action<string> _setStatus;

    public ProtectedFilesPage(Action<string> setStatus)
    {
        InitializeComponent();
        _setStatus = setStatus;
        _protectedService = new(_settingsService, _modService);
        Refresh();
    }

    private void Refresh()
    {
        ProtectedList.DisplayMemberPath = "DisplayLabel";
        ProtectedList.ItemsSource = _protectedService.Load().OrderBy(item => item.RelativePath).ToList();
    }

    private void ProtectFiles_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        if (!EnsurePluginsFolder(settings)) return;
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "All files (*.*)|*.*", InitialDirectory = _modService.GetPluginsPath(settings), Title = "Select files inside Plugins" };
        if (dialog.ShowDialog() != true) return;
        Protect(settings, dialog.FileNames);
    }

    private void ProtectFolder_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        if (!EnsurePluginsFolder(settings)) return;
        var dialog = new OpenFolderDialog { InitialDirectory = _modService.GetPluginsPath(settings), Title = "Select a folder inside Plugins" };
        if (dialog.ShowDialog() != true) return;
        Protect(settings, [dialog.FolderName]);
    }

    private void Protect(Casualties_Hub.Models.Settings settings, IEnumerable<string> paths)
    {
        try
        {
            _protectedService.Protect(settings, paths);
            Refresh();
            DebugLogService.Info("Protected item(s) saved locally.");
            _setStatus("Protected item(s) saved locally.");
        }
        catch (Exception ex)
        {
            DebugLogService.Error("Protect items failed", ex);
            MessageBox.Show(ex.Message, "Could not protect item", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        if (!EnsurePluginsFolder(settings)) return;
        if (MessageBox.Show("Restore All permanently removes the current version of each protected file or folder, then replaces it with the saved copy. Continue?", "Restore protected items", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            var count = _protectedService.Restore(settings);
            DebugLogService.Info($"Restored {count} protected item(s).");
            _setStatus($"Restored {count} protected item(s).");
            MessageBox.Show($"Restored {count} protected item(s).", "Restore complete");
        }
        catch (Exception ex)
        {
            DebugLogService.Error("Restore protected items failed", ex);
            MessageBox.Show(ex.Message, "Restore failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (ProtectedList.SelectedItem is not Casualties_Hub.Models.ProtectedFile item)
        {
            MessageBox.Show("Select a protected file or folder first.", "Protected Assets", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"Remove the saved copy of '{item.RelativePath}'? This does not delete the live game file.", "Remove protected item", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            _protectedService.Remove(item);
            Refresh();
            DebugLogService.Activity("Protected Assets", $"Removed protected item {item.RelativePath} from the page.");
            _setStatus("Protected item removed.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not remove protected item", exception);
            MessageBox.Show(exception.Message, "Could not remove protected item", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenProtectedFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = _protectedService.GetProtectedRoot();
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        DebugLogService.Activity("Protected Assets", "Opened the protected-assets storage folder.");
    }

    private bool EnsurePluginsFolder(Casualties_Hub.Models.Settings settings)
    {
        if (_modService.HasConfiguredPluginsFolder(settings)) return true;
        DebugLogService.Activity("Protected Assets", "Blocked an action because no valid Plugins folder is configured.");
        MessageBox.Show("Set a game, BepInEx, or Plugins folder that resolves to a valid BepInEx\\Plugins folder first.", "Casualties Hub");
        return false;
    }
}
