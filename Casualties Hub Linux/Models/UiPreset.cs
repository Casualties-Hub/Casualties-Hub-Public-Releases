namespace Casualties_Hub.Models;

/// <summary>
/// Identifies which set of colours is loaded. Custom slots are "Custom1".."Custom4".
/// Animated RGB is tracked separately, as a switch over whichever colours are
/// loaded, so turning it off never discards the player's own colours.
/// </summary>
public static class UiPresetIds
{
    public const string Default = "Default";

    /// <summary>Colours the player mixed by hand rather than loading from a slot.</summary>
    public const string CustomColours = "CustomColours";

    /// <summary>How many slots a player can save their own looks into.</summary>
    public const int CustomSlotCount = 4;

    public static string Custom(int slot) => $"Custom{slot}";

    /// <summary>Returns the 1-based slot number for a custom preset id.</summary>
    public static bool TryGetCustomSlot(string? presetId, out int slot)
    {
        slot = 0;
        if (presetId is null || !presetId.StartsWith("Custom", StringComparison.Ordinal)) return false;
        return int.TryParse(presetId.AsSpan("Custom".Length), out slot)
            && slot >= 1
            && slot <= CustomSlotCount;
    }
}

/// <summary>
/// A saved look: the four themed colours plus the text size. Animated RGB is not
/// stored here because it drives the colours live rather than from saved values.
/// </summary>
public sealed class UiPreset
{
    public string Name { get; set; } = "";

    /// <summary>False while a custom slot has never been written to.</summary>
    public bool IsSaved { get; set; }

    // These are the stock look and must stay in step with the matching defaults
    // on Settings, or ReconcileActivePreset will label a clean install "Custom".
    public byte PrimaryTextRed { get; set; } = 241;
    public byte PrimaryTextGreen { get; set; } = 239;
    public byte PrimaryTextBlue { get; set; } = 238;
    public byte BackgroundRed { get; set; } = 20;
    public byte BackgroundGreen { get; set; } = 20;
    public byte BackgroundBlue { get; set; } = 20;
    public byte SurfaceRed { get; set; } = 30;
    public byte SurfaceGreen { get; set; } = 30;
    public byte SurfaceBlue { get; set; } = 30;
    public byte AccentRed { get; set; } = 200;
    public byte AccentGreen { get; set; } = 30;
    public byte AccentBlue { get; set; } = 60;
    public double TextSize { get; set; } = 14;

    /// <summary>The stock Casualties Hub look; the property defaults above are it.</summary>
    public static UiPreset Stock => new() { Name = "Default", IsSaved = true };

    public static UiPreset Capture(Settings settings, string name) => new()
    {
        Name = name,
        IsSaved = true,
        PrimaryTextRed = settings.PrimaryTextRed,
        PrimaryTextGreen = settings.PrimaryTextGreen,
        PrimaryTextBlue = settings.PrimaryTextBlue,
        BackgroundRed = settings.BackgroundRed,
        BackgroundGreen = settings.BackgroundGreen,
        BackgroundBlue = settings.BackgroundBlue,
        SurfaceRed = settings.SurfaceRed,
        SurfaceGreen = settings.SurfaceGreen,
        SurfaceBlue = settings.SurfaceBlue,
        AccentRed = settings.AccentRed,
        AccentGreen = settings.AccentGreen,
        AccentBlue = settings.AccentBlue,
        TextSize = settings.TextSize
    };

    /// <summary>Copies the colours only, leaving the player's text size alone.</summary>
    public void ApplyColoursTo(Settings settings)
    {
        settings.PrimaryTextRed = PrimaryTextRed;
        settings.PrimaryTextGreen = PrimaryTextGreen;
        settings.PrimaryTextBlue = PrimaryTextBlue;
        settings.BackgroundRed = BackgroundRed;
        settings.BackgroundGreen = BackgroundGreen;
        settings.BackgroundBlue = BackgroundBlue;
        settings.SurfaceRed = SurfaceRed;
        settings.SurfaceGreen = SurfaceGreen;
        settings.SurfaceBlue = SurfaceBlue;
        settings.AccentRed = AccentRed;
        settings.AccentGreen = AccentGreen;
        settings.AccentBlue = AccentBlue;
        settings.ThemeColoursInitialized = true;
        settings.RebrandThemeInitialized = true;
    }

    public void ApplyTo(Settings settings)
    {
        ApplyColoursTo(settings);
        settings.TextSize = TextSize;
    }
}
