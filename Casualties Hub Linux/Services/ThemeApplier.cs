using Avalonia;
using Avalonia.Media;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Pushes the palette derived from the player's colours into the application resources, so every
/// DynamicResource binding in the UI updates at once.
/// </summary>
/// <remarks>
/// The Windows Hub does the same thing by assigning into Application.Current.Resources, and
/// Avalonia propagates DynamicResource the same way, so this is one of the few pieces of the
/// theming layer that ports almost unchanged.
/// </remarks>
public static class ThemeApplier
{
    public static void Apply(Settings settings)
    {
        var application = Application.Current;
        if (application is null) return;

        foreach (var (key, colour) in ThemePalette.Build(settings))
            application.Resources[key] = new SolidColorBrush(colour);

        // Not derived from the four user colours: these carry fixed meaning (a success is green
        // whatever the theme) so they are only defined if a starting value did not already exist.
        foreach (var (key, fallback) in new (string, Color)[]
                 {
                     ("SuccessBrush", Color.FromRgb(0x3F, 0xB9, 0x50)),
                     ("WarningBrush", Color.FromRgb(0xD2, 0x99, 0x22)),
                 })
        {
            if (!application.Resources.ContainsKey(key))
                application.Resources[key] = new SolidColorBrush(fallback);
        }

        DebugLogService.Activity("Theme", "Applied the saved colour palette.");
    }

    /// <summary>
    /// Applies the saved text size to the whole shell.
    /// </summary>
    /// <remarks>
    /// Set on the window rather than per control: FontSize inherits down the tree in Avalonia as
    /// it does in WPF, so one assignment resizes every page. Without this the Settings slider
    /// saves a value that nothing ever reads.
    /// </remarks>
    public static void ApplyTextSize(Settings settings)
    {
        if (Application.Current?.ApplicationLifetime
            is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) return;

        // Clamped the same way SettingsService clamps it, so a hand-edited Settings.json cannot
        // produce an unreadable or comically large interface.
        var size = Math.Clamp(settings.TextSize, 10, 20);
        if (desktop.MainWindow is { } window) window.FontSize = size;
    }
}
