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

    public byte PrimaryTextRed { get; set; } = 194;
    public byte PrimaryTextGreen { get; set; } = 31;
    public byte PrimaryTextBlue { get; set; } = 50;
    public byte ButtonTextRed { get; set; } = 20;
    public byte ButtonTextGreen { get; set; } = 20;
    public byte ButtonTextBlue { get; set; } = 20;
    public byte NavigationSurfaceRed { get; set; } = 245;
    public byte NavigationSurfaceGreen { get; set; } = 245;
    public byte NavigationSurfaceBlue { get; set; } = 245;
    public byte AccentRed { get; set; } = 194;
    public byte AccentGreen { get; set; } = 31;
    public byte AccentBlue { get; set; } = 50;
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
        ButtonTextRed = settings.ButtonTextRed,
        ButtonTextGreen = settings.ButtonTextGreen,
        ButtonTextBlue = settings.ButtonTextBlue,
        NavigationSurfaceRed = settings.NavigationSurfaceRed,
        NavigationSurfaceGreen = settings.NavigationSurfaceGreen,
        NavigationSurfaceBlue = settings.NavigationSurfaceBlue,
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
        settings.ButtonTextRed = ButtonTextRed;
        settings.ButtonTextGreen = ButtonTextGreen;
        settings.ButtonTextBlue = ButtonTextBlue;
        settings.NavigationSurfaceRed = NavigationSurfaceRed;
        settings.NavigationSurfaceGreen = NavigationSurfaceGreen;
        settings.NavigationSurfaceBlue = NavigationSurfaceBlue;
        settings.AccentRed = AccentRed;
        settings.AccentGreen = AccentGreen;
        settings.AccentBlue = AccentBlue;
        settings.ThemeColoursInitialized = true;
    }

    public void ApplyTo(Settings settings)
    {
        ApplyColoursTo(settings);
        settings.TextSize = TextSize;
    }
}
