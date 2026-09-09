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
    private static Settings? _settings;
    private static DateTime _startedUtc;

    public static bool IsRunning => _timer is not null;

    public static void Start(SettingsService settingsService) => Start(settingsService, settingsService.Load());

    private static void Start(SettingsService settingsService, Settings settings)
    {
        if (_timer is not null) return;

        _settingsService = settingsService;
        _settings = settings;
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
        _settings = null;

        if (_settingsService is not null) ThemeApplier.Apply(_settingsService.Load());
        DebugLogService.Activity("Theme", "Animated RGB stopped.");
    }

    /// <summary>
    /// Starts or stops the sweep to match the saved setting. Call after every settings save: the
    /// sweep works from a copy taken here, so a colour changed while it runs is picked up.
    /// </summary>
    public static void Sync(SettingsService settingsService)
    {
        var settings = settingsService.Load();
        if (!settings.AnimatedRgbEnabled)
        {
            Stop();
            return;
        }

        if (_timer is null) Start(settingsService, settings);
        else _settings = settings;
    }

    private static void Tick(object? sender, EventArgs e)
    {
        if (_settings is null) return;

        try
        {
            var elapsed = (DateTime.UtcNow - _startedUtc).TotalSeconds;
            var hue = elapsed % CycleSeconds / CycleSeconds * 360.0;
            var (r, g, b) = ColourWheel.FromHsv(hue, 0.85, 0.85);

            // Only the accent moves. Text, background and panels stay as the player set them,
            // or the shell would strobe unreadably. The copy is never saved, so the accent the
            // player chose is untouched on disk.
            _settings.AccentRed = r;
            _settings.AccentGreen = g;
            _settings.AccentBlue = b;

            ThemeApplier.Apply(_settings);
        }
        catch (Exception exception)
        {
            // A failure here would otherwise repeat every tick and flood the log.
            DebugLogService.Error("Animated RGB stopped after an error", exception);
            Stop();
        }
    }
}
