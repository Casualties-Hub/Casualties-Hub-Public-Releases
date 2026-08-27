using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>Hosts <see cref="DebugPage"/>. Opened by clicking the sidebar version five times.</summary>
public partial class DebugWindow : Window
{
    private static DebugWindow? _open;

    public DebugWindow()
    {
        AvaloniaXamlLoader.Load(this);

        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://casualties-hub/Assets/CasualtiesHub.png"));
            Icon = new WindowIcon(Bitmap.DecodeToWidth(stream, 64));
        }
        catch (Exception exception)
        {
            DebugLogService.Info($"Developer Console icon could not be loaded: {exception.Message}");
        }

        // No status bar here, so the page's messages go to the log.
        this.FindControl<ContentControl>("ConsoleHost")!.Content =
            new DebugPage(message => DebugLogService.Info(message));
    }

    /// <summary>Shows the console, or focuses the existing one. Two would double-handle the log.</summary>
    public static void ShowFor(Window owner)
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        var window = new DebugWindow();
        _open = window;
        window.Closed += (_, _) => _open = null;
        window.Show(owner);
        DebugLogService.Activity("Developer Console", "Opened from the version shortcut.");
    }
}
