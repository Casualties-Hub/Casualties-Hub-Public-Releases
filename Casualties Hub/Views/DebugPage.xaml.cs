using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class DebugPage : Page
{
    private readonly Action<string> _setStatus;
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();

    public DebugPage(Action<string> setStatus)
    {
        InitializeComponent();
        _setStatus = setStatus;
        LogList.ItemsSource = DebugLogService.Entries;
        Loaded += DebugPage_Loaded;
        Unloaded += DebugPage_Unloaded;
    }

    private void DebugPage_Loaded(object sender, RoutedEventArgs e)
    {
        DebugLogService.Entries.CollectionChanged += Entries_CollectionChanged;
        DebugLogService.Info("Developer Console opened.");
        Refresh();
    }

    private void DebugPage_Unloaded(object sender, RoutedEventArgs e) =>
        DebugLogService.Entries.CollectionChanged -= Entries_CollectionChanged;

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        LiveStatusText.Text = $"Live activity: On — {DebugLogService.Entries.Count} entries this session";
        if (AutoScrollBox.IsChecked == true && DebugLogService.Entries.Count > 0)
            Dispatcher.BeginInvoke(() => LogList.ScrollIntoView(DebugLogService.Entries[0]));
    }

    private void Refresh()
    {
        LiveStatusText.Text = $"Live activity: On — {DebugLogService.Entries.Count} entries this session";
        if (AutoScrollBox.IsChecked == true && DebugLogService.Entries.Count > 0)
            LogList.ScrollIntoView(DebugLogService.Entries[0]);
        _setStatus($"Developer Console: {DebugLogService.Entries.Count} live entries.");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        DebugLogService.Clear();
        Refresh();
    }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, DebugLogService.Entries));
            _setStatus("Copied Developer Console text to the clipboard.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not copy Developer Console text", exception);
            MessageBox.Show(exception.Message, "Developer Console", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenCurrentLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(DebugLogService.LogDirectory);
            if (!File.Exists(DebugLogService.CurrentSessionLogPath))
                File.WriteAllText(DebugLogService.CurrentSessionLogPath, string.Empty);
            Process.Start(new ProcessStartInfo("notepad.exe", "\"" + DebugLogService.CurrentSessionLogPath + "\"") { UseShellExecute = true });
            _setStatus("Opened the current Hub log in Notepad.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not open the current Hub log", exception);
            MessageBox.Show(exception.Message, "Developer Console", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenCrashLog_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.Load();
        if (!_modService.HasConfiguredGameFolder(settings))
        {
            MessageBox.Show("Set the game folder first.", "BepInEx crash log", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var bepinex = Path.Combine(_modService.GetGameRoot(settings), "BepInEx");
        var logPath = new[] { "LogOut.log", "LogOutput.log" }.Select(file => Path.Combine(bepinex, file)).FirstOrDefault(File.Exists);
        if (logPath is null)
        {
            MessageBox.Show("No BepInEx LogOut.log or LogOutput.log was found yet.", "BepInEx crash log", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo("notepad.exe", "\"" + logPath + "\"") { UseShellExecute = true });
        _setStatus("Opened BepInEx crash log in Notepad.");
    }

    private void CreateCrashReport_Click(object sender, RoutedEventArgs e)
    {
        var path = DebugLogService.CreateCrashReport();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("The Hub could not create a crash report.", "Crash Report", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = true });
        _setStatus("Created a support-ready Crash Report.");
    }

    private void OpenHubLogs_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(DebugLogService.LogDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", "\"" + DebugLogService.LogDirectory + "\"") { UseShellExecute = true });
    }
}
