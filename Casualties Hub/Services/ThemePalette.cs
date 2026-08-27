using System.Windows.Media;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Turns the four colours a player can set (text, background, panels, accent)
/// into every brush the shell needs. Keeping the derivation in one place is what
/// lets a player recolour the background without leaving unreadable buttons,
/// borders, or muted labels behind.
/// </summary>
public static class ThemePalette
{
    public static Color Text(Settings s) => Color.FromRgb(s.PrimaryTextRed, s.PrimaryTextGreen, s.PrimaryTextBlue);
    public static Color Background(Settings s) => Color.FromRgb(s.BackgroundRed, s.BackgroundGreen, s.BackgroundBlue);
    public static Color Surface(Settings s) => Color.FromRgb(s.SurfaceRed, s.SurfaceGreen, s.SurfaceBlue);
    public static Color Accent(Settings s) => Color.FromRgb(s.AccentRed, s.AccentGreen, s.AccentBlue);

    /// <summary>Perceived brightness, 0..1, using the usual luma weights.</summary>
    public static double Luma(Color c) => ((c.R * 0.299) + (c.G * 0.587) + (c.B * 0.114)) / 255;

    public static bool IsLight(Color c) => Luma(c) >= 0.55;

    /// <summary>Black or white, whichever stays readable on <paramref name="background"/>.</summary>
    public static Color ContrastText(Color background) => IsLight(background) ? Color.FromRgb(20, 20, 20) : Colors.White;

    /// <summary>
    /// The more readable of near-black and white on <paramref name="background"/>,
    /// measured by real contrast ratio rather than a brightness cutoff. This is
    /// what keeps the label inside a button legible while Animated RGB sweeps the
    /// accent through hues a simple luma test gets wrong (saturated yellows and
    /// cyans especially).
    /// </summary>
    public static Color ReadableOn(Color background) =>
        ContrastRatio(HardLight, background) >= ContrastRatio(HardDark, background) ? HardLight : HardDark;

    /// <summary>
    /// The house style for an accent button, taken from the sidebar pill: a dark
    /// wash of the accent with the accent itself as the label, rather than a
    /// solid slab of colour.
    /// </summary>
    /// <remarks>
    /// The label is nudged *away from the wash* rather than through Shift. Shift
    /// darkens anything it considers light, so a light accent (a saturated yellow
    /// or cyan, which Animated RGB sweeps through) was being pushed darker
    /// against an already dark wash: contrast fell instead of rising and the loop
    /// never converged.
    /// </remarks>
    public static (Color Soft, Color OnSoft) AccentButton(Color accent, Color background)
    {
        var soft = Mix(accent, background, 0.72);
        var away = IsLight(soft) ? Colors.Black : Colors.White;
        var onSoft = accent;
        for (var attempt = 0; attempt < 12 && ContrastRatio(onSoft, soft) < 4.5; attempt++)
            onSoft = Mix(onSoft, away, 0.12);
        return (soft, onSoft);
    }

    private static readonly Color HardLight = Colors.White;
    private static readonly Color HardDark = Color.FromRgb(20, 20, 20);

    /// <summary>WCAG relative luminance.</summary>
    private static double RelativeLuminance(Color c)
    {
        static double Linear(double channel)
        {
            channel /= 255;
            return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        return (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));
    }

    /// <summary>WCAG contrast ratio, 1 (identical) to 21 (black on white).</summary>
    public static double ContrastRatio(Color a, Color b)
    {
        var first = RelativeLuminance(a);
        var second = RelativeLuminance(b);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    /// <summary>
    /// Honours the colour a player picked for text, but never at the cost of
    /// readability: if it does not clear <paramref name="minimumRatio"/> against
    /// every surface it has to sit on, it is replaced by whichever of near-black
    /// or white reads better. This is what stops a background and text choice
    /// from combining into an unusable window.
    /// </summary>
    public static Color ReadableText(Color desired, double minimumRatio, params Color[] surfaces)
    {
        if (surfaces.Length == 0) return desired;
        if (surfaces.All(surface => ContrastRatio(desired, surface) >= minimumRatio)) return desired;

        var lightWorst = surfaces.Min(surface => ContrastRatio(HardLight, surface));
        var darkWorst = surfaces.Min(surface => ContrastRatio(HardDark, surface));
        return lightWorst >= darkWorst ? HardLight : HardDark;
    }

    /// <summary>
    /// Fades <paramref name="text"/> toward <paramref name="background"/> for a
    /// secondary label, backing off the fade until it still clears
    /// <paramref name="minimumRatio"/> so muted text never disappears.
    /// </summary>
    private static Color MutedAgainst(Color text, Color background, double fade, double minimumRatio)
    {
        for (var amount = fade; amount > 0; amount -= 0.08)
        {
            var candidate = Mix(text, background, amount);
            if (ContrastRatio(candidate, background) >= minimumRatio) return candidate;
        }
        return text;
    }

    /// <summary>
    /// Moves a colour toward white or black by <paramref name="amount"/> (0..1).
    /// Dark themes step up, light themes step down, so "one shade further from
    /// the page" means the same thing either way.
    /// </summary>
    public static Color Shift(Color c, double amount)
    {
        var target = IsLight(c) ? 0d : 255d;
        return Color.FromRgb(
            Channel(c.R + ((target - c.R) * amount)),
            Channel(c.G + ((target - c.G) * amount)),
            Channel(c.B + ((target - c.B) * amount)));
    }

    /// <summary>Blends <paramref name="from"/> toward <paramref name="to"/>.</summary>
    public static Color Mix(Color from, Color to, double amount) => Color.FromRgb(
        Channel(from.R + ((to.R - from.R) * amount)),
        Channel(from.G + ((to.G - from.G) * amount)),
        Channel(from.B + ((to.B - from.B) * amount)));

    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    /// <summary>
    /// Every themed brush key, derived from the player's four colours. The keys
    /// match the resource names declared in App.xaml.
    /// </summary>
    public static Dictionary<string, Color> Build(Settings settings)
    {
        var text = Text(settings);
        var background = Background(settings);
        var surface = Surface(settings);
        var accent = Accent(settings);

        // Buttons sit one step further from the page than a card does, so they
        // stay visible whether the player picked a dark or a light background.
        var controlSurface = Shift(surface, 0.14);

        // Body text has to work on the page and on panels, so it is checked
        // against both. A washed-out choice is overridden rather than obeyed.
        var readableText = ReadableText(text, 4.5, background, surface);

        var (accentSoft, accentOnSoft) = AccentButton(accent, background);

        return new Dictionary<string, Color>
        {
            ["PrimaryTextBrush"] = readableText,
            ["AccentBrush"] = accent,
            ["AccentTextBrush"] = ReadableOn(accent),
            ["AccentSoftBrush"] = accentSoft,
            ["AccentSoftTextBrush"] = accentOnSoft,
            ["WindowBackgroundBrush"] = background,
            // Chrome is the sidebar, top bar, and status bar. It must sit *behind*
            // the page, so it is always darkened rather than shifted: on a dark
            // theme Shift would push it toward white and produce a grey sidebar
            // that washes out the whole window.
            ["ChromeBrush"] = Mix(background, Colors.Black, IsLight(background) ? 0.08 : 0.35),
            ["CardBrush"] = surface,
            // Sunken: recessed back toward the page (path boxes, list wells).
            ["InsetBrush"] = Mix(surface, background, 0.70),
            // Raised: an item sitting on top of a panel (mod cards, sub-panels).
            ["RaisedBrush"] = Shift(surface, 0.05),
            ["CardBorderBrush"] = Shift(surface, 0.10),
            ["DividerBrush"] = Shift(surface, 0.10),
            ["InputBackgroundBrush"] = Shift(surface, 0.06),
            ["InputBorderBrush"] = Shift(surface, 0.20),
            ["ControlSurfaceBrush"] = controlSurface,
            ["ControlTextBrush"] = ReadableText(readableText, 4.5, controlSurface),
            ["ControlBorderBrush"] = Shift(surface, 0.26),
            ["ControlHoverBrush"] = Shift(surface, 0.22),
            ["ControlPressedBrush"] = Shift(surface, 0.30),
            // Muted labels fade the page text toward the background rather than
            // using a fixed grey, so they follow a recoloured theme. The fade is
            // reduced automatically if it would push them below readable.
            ["MutedTextBrush"] = MutedAgainst(readableText, background, 0.40, 4.5),
            ["DimTextBrush"] = MutedAgainst(readableText, background, 0.58, 3.0)
        };
    }
}
