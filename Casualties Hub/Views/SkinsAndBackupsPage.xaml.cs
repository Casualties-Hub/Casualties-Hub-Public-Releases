using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// The former Protected Assets and Skin Preview pages, merged into the single
/// "Skins &amp; Backups" section. Both halves keep their own service and refresh
/// path; they share a page because they are the two things you do to installed
/// character art.
/// </summary>
public partial class SkinsAndBackupsPage : Page
{
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly ProtectedFilesService _protectedService;
    private readonly SkinLibraryService _skinLibrary;
    private readonly Action<string> _setStatus;

    public SkinsAndBackupsPage(Action<string> setStatus)
    {
        InitializeComponent();
        _setStatus = setStatus;
        _protectedService = new(_settingsService, _modService);
        _skinLibrary = new(_settingsService, _modService);
        RefreshProtected();
        RefreshSkins();
    }

    // ----- Backups -----

    private void RefreshProtected()
    {
        ProtectedList.DisplayMemberPath = "DisplayLabel";
        var items = _protectedService.Load().OrderBy(item => item.RelativePath).ToList();
        ProtectedList.ItemsSource = items;
        NoProtectedMessage.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

    private void Protect(Settings settings, IEnumerable<string> paths)
    {
        try
        {
            _protectedService.Protect(settings, paths);
            RefreshProtected();
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
        if (ProtectedList.SelectedItem is not ProtectedFile item)
        {
            MessageBox.Show("Select a protected file or folder first.", "Backups", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"Remove the saved copy of '{item.RelativePath}'? This does not delete the live game file.", "Remove protected item", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            _protectedService.Remove(item);
            RefreshProtected();
            DebugLogService.Activity("Backups", $"Removed protected item {item.RelativePath} from the page.");
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
        DebugLogService.Activity("Backups", "Opened the protected-assets storage folder.");
    }

    private bool EnsurePluginsFolder(Settings settings)
    {
        if (_modService.HasConfiguredPluginsFolder(settings)) return true;
        DebugLogService.Activity("Backups", "Blocked an action because no valid Plugins folder is configured.");
        MessageBox.Show("Set a game, BepInEx, or Plugins folder that resolves to a valid BepInEx\\Plugins folder first.", "Casualties Hub");
        return false;
    }

    // ----- Skin Preview -----

    private void RefreshSkins()
    {
        var settings = _settingsService.Load();
        if (!_modService.HasConfiguredPluginsFolder(settings))
        {
            ShowEmptySlots("Set a game, BepInEx, or Plugins folder in Settings before skins can be detected.");
            return;
        }

        var slots = _skinLibrary.DiscoverSlots();
        if (slots.Count == 0)
        {
            ShowEmptySlots("No CustomSprites skin slots were found. Install a character skin, then choose Refresh.");
            return;
        }

        SlotList.ItemsSource = slots;
        SlotList.Visibility = Visibility.Visible;
        EmptySlotsMessage.Visibility = Visibility.Collapsed;
        SlotList.SelectedIndex = 0;
        DebugLogService.Activity("Skin Preview", $"Detected {slots.Count} CustomSprites slot(s).");
    }

    private void ShowEmptySlots(string message)
    {
        SlotList.ItemsSource = null;
        SlotList.Visibility = Visibility.Collapsed;
        EmptySlotsMessage.Text = message;
        EmptySlotsMessage.Visibility = Visibility.Visible;
        Preview.Clear();
    }

    private void SlotList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SlotList.SelectedItem is not SkinSlot slot)
        {
            Preview.Clear();
            return;
        }
        Preview.LoadSkin(slot.FolderPath);
        _setStatus($"Previewing skin slot {slot.Name.ToUpperInvariant()}.");
    }

    private void RefreshSkins_Click(object sender, RoutedEventArgs e)
    {
        RefreshSkins();
        _setStatus("Skin slots refreshed.");
    }

    private void SkinSite_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        DebugLogService.Activity("Skin Preview", "Opened the community skin site.");
        e.Handled = true;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var root = _skinLibrary.GetCustomSpritesRoot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            MessageBox.Show("The CustomSprites folder was not found inside the configured Plugins folder.", "Skin Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{root}\"") { UseShellExecute = true });
        DebugLogService.Activity("Skin Preview", "Opened the CustomSprites folder.");
    }
}
