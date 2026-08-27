using System.Collections.Specialized;
using System.IO;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class DebugPage : UserControl
{
    private readonly Action<string> _setStatus;
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();

    public DebugPage() : this(_ => { }) { }

    public DebugPage(Action<string> setStatus)
    {
        _setStatus = setStatus;
        AvaloniaXamlLoader.Load(this);

        var logList = this.FindControl<ListBox>("LogList")!;
        logList.ItemsSource = DebugLogService.Entries;

        this.FindControl<Button>("RefreshButton")!.Click += (_, _) => Refresh();
        this.FindControl<Button>("ClearButton")!.Click += (_, _) => { DebugLogService.Clear(); Refresh(); };
        this.FindControl<Button>("CopyLogButton")!.Click += async (_, _) => await CopyLogAsync();
        this.FindControl<Button>("OpenCurrentLogButton")!.Click += (_, _) => OpenCurrentLog();
        this.FindControl<Button>("OpenCrashLogButton")!.Click += async (_, _) => await OpenBepInExLogAsync();
        this.FindControl<Button>("CreateCrashReportButton")!.Click += async (_, _) => await CreateCrashReportAsync();
        this.FindControl<Button>("OpenLogsFolderButton")!.Click += (_, _) => LinuxShell.OpenFolder(DebugLogService.LogDirectory);

        // The log service outlives every page, so an attached handler would pin this one.
        AttachedToVisualTree += (_, _) =>
        {
            DebugLogService.Entries.CollectionChanged += OnEntriesChanged;
            DebugLogService.Info("Developer Console opened.");
            Refresh();
        };
        DetachedFromVisualTree += (_, _) => DebugLogService.Entries.CollectionChanged -= OnEntriesChanged;
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateLiveCount();
        ScrollToNewest();
    }

    private void UpdateLiveCount() =>
        this.FindControl<TextBlock>("LiveStatusText")!.Text =
            $"Live activity: On — {DebugLogService.Entries.Count} entries this session";

    /// <summary>Newest entries are inserted at index 0, so newest means the top.</summary>
    private void ScrollToNewest()
    {
        if (this.FindControl<CheckBox>("AutoScrollBox")!.IsChecked != true) return;
        if (DebugLogService.Entries.Count == 0) return;
        this.FindControl<ListBox>("LogList")!.ScrollIntoView(0);
    }

    private void Refresh()
    {
        UpdateLiveCount();
        ScrollToNewest();
        _setStatus($"Developer Console: {DebugLogService.Entries.Count} live entries.");
    }

    private async Task CopyLogAsync()
    {
        var text = string.Join(Environment.NewLine, DebugLogService.Entries);
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            _setStatus("No clipboard is available on this desktop.");
            return;
        }

        try
        {
            await clipboard.SetTextAsync(text);
            _setStatus($"Copied {DebugLogService.Entries.Count} log entries to the clipboard.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not copy the console text", exception);
            _setStatus("Could not copy the log to the clipboard.");
        }
    }

    private void OpenCurrentLog()
    {
        try
        {
            Directory.CreateDirectory(DebugLogService.LogDirectory);
            if (!File.Exists(DebugLogService.CurrentSessionLogPath))
                File.WriteAllText(DebugLogService.CurrentSessionLogPath, string.Empty);

            LinuxShell.OpenFile(DebugLogService.CurrentSessionLogPath);
            _setStatus("Opened this session's Hub log.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not open the current Hub log", exception);
            _setStatus("Could not open the current Hub log.");
        }
    }

    private async Task OpenBepInExLogAsync()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var settings = _settingsService.Load();
        if (!_modService.HasConfiguredGameFolder(settings))
        {
            if (owner is not null)
                await HubDialog.ShowMessageAsync(owner, "BepInEx crash log", "Set the game folder in Settings first.");
            return;
        }

        // Both spellings exist in the wild depending on the BepInEx version.
        var bepInEx = Path.Combine(_modService.GetGameRoot(settings), "BepInEx");
        var logPath = new[] { "LogOutput.log", "LogOut.log" }
            .Select(name => Path.Combine(bepInEx, name))
            .FirstOrDefault(File.Exists);

        if (logPath is null)
        {
            if (owner is not null)
                await HubDialog.ShowMessageAsync(owner, "BepInEx crash log",
                    "No BepInEx LogOutput.log or LogOut.log was found yet. Run the game once with mods installed.");
            return;
        }

        LinuxShell.OpenFile(logPath);
        _setStatus("Opened the BepInEx crash log.");
    }

    private async Task CreateCrashReportAsync()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var path = DebugLogService.CreateCrashReport();

        if (string.IsNullOrWhiteSpace(path))
        {
            if (owner is not null)
                await HubDialog.ShowMessageAsync(owner, "Crash report", "The Hub could not create a crash report.");
            return;
        }

        LinuxShell.OpenFile(path);
        _setStatus("Created a support-ready crash report.");
    }
}
