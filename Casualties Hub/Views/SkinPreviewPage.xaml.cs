using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class SkinPreviewPage : Page
{
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly SkinLibraryService _skinLibrary;
    private readonly Action<string> _setStatus;

    public SkinPreviewPage(Action<string> setStatus)
    {
        InitializeComponent();
        _setStatus = setStatus;
        _skinLibrary = new(_settingsService, _modService);
        Refresh();
    }

    private void Refresh()
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

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        Refresh();
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
