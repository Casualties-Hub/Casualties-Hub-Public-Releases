using Avalonia.Threading;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Sweeps the accent colour through the hue circle while Animated RGB is switched on.
/// </summary>
/// <remarks>
/// Runs at 20 FPS and advances by wall-clock time rather than per tick, so the visible speed does
/// not depend on the frame rate. That matters where the machine falls back to software rendering
/// (llvmpipe under a VM), because repainting the whole shell more often is wasteful there.
///
/// The player's saved colours are never overwritten: the sweep only pushes brushes into the
/// application resources, so switching it off restores the stored palette untouched.
/// </remarks>
public static class AnimatedRgbDriver
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    /// <summary>Seconds for one full trip around the hue circle.</summary>
    private const double CycleSeconds = 12.0;

    private static DispatcherTimer? _timer;
    private static SettingsService? _settingsService;
    private static DateTime _startedUtc;

    public static bool IsRunning => _timer is not null;

    public static void Start(SettingsService settingsService)
    {
        if (_timer is not null) return;

        _settingsService = settingsService;
        _startedUtc = DateTime.UtcNow;
        _timer = new DispatcherTimer(Interval, DispatcherPriority.Background, Tick);
        _timer.Start();
        DebugLogService.Activity("Theme", "Animated RGB started.");
    }

    /// <summary>Stops the sweep and puts the saved colours back.</summary>
    public static void Stop()
    {
        if (_timer is null) return;

        _timer.Stop();
        _timer = null;

        if (_settingsService is not null) ThemeApplier.Apply(_settingsService.Load());
        DebugLogService.Activity("Theme", "Animated RGB stopped.");
    }

    public static void Sync(SettingsService settingsService)
    {
        if (settingsService.Load().AnimatedRgbEnabled) Start(settingsService);
        else Stop();
    }

    private static void Tick(object? sender, EventArgs e)
    {
        if (_settingsService is null) return;

        try
        {
            var elapsed = (DateTime.UtcNow - _startedUtc).TotalSeconds;
            var hue = elapsed % CycleSeconds / CycleSeconds * 360.0;
            var (r, g, b) = ColourWheel.FromHsv(hue, 0.85, 0.85);

            // Only the accent moves. Text, background and panels stay as the player set them,
            // or the shell would strobe unreadably.
            var settings = _settingsService.Load();
            settings.AccentRed = r;
            settings.AccentGreen = g;
            settings.AccentBlue = b;

            ThemeApplier.Apply(settings);
        }
        catch (Exception exception)
        {
            // A failure here would otherwise repeat every tick and flood the log.
            DebugLogService.Error("Animated RGB stopped after an error", exception);
            Stop();
        }
    }
}
