using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Casualties_Hub.Services;

/// <summary>
/// The HSV colour wheel on the Settings page: hue around the circle, saturation from the centre out.
/// </summary>
/// <remarks>
/// The Windows Hub paints this with WriteableBitmap.WritePixels. Avalonia exposes the same idea
/// through <see cref="ILockedFramebuffer"/> instead, so the maths is unchanged and only the way
/// pixels are written differs. Premultiplied BGRA is used because that is what Avalonia's default
/// pixel format expects; writing straight BGRA would tint the edges.
/// </remarks>
public static class ColourWheel
{
    /// <summary>Renders a wheel of the given pixel size at full brightness.</summary>
    public static WriteableBitmap Render(int size)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        using var buffer = bitmap.Lock();
        var radius = size / 2.0;

        unsafe
        {
            for (var y = 0; y < size; y++)
            {
                var row = (byte*)buffer.Address + y * buffer.RowBytes;
                for (var x = 0; x < size; x++)
                {
                    var dx = x - radius + 0.5;
                    var dy = y - radius + 0.5;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    var pixel = row + x * 4;

                    if (distance > radius)
                    {
                        // Outside the circle: fully transparent. Premultiplied, so every
                        // channel must be zero, not just alpha.
                        pixel[0] = pixel[1] = pixel[2] = pixel[3] = 0;
                        continue;
                    }

                    var hue = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
                    var saturation = Math.Min(1.0, distance / radius);
                    var (r, g, b) = FromHsv(hue, saturation, 1.0);

                    // Feather the last pixel of the edge so the circle is not visibly jagged.
                    var alpha = (byte)Math.Clamp((radius - distance) * 255, 0, 255);
                    pixel[0] = (byte)(b * alpha / 255);
                    pixel[1] = (byte)(g * alpha / 255);
                    pixel[2] = (byte)(r * alpha / 255);
                    pixel[3] = alpha;
                }
            }
        }

        return bitmap;
    }

    /// <summary>The colour at a point on the wheel, or null when the point is outside the circle.</summary>
    public static Color? Sample(double x, double y, double size, double value)
    {
        var radius = size / 2.0;
        var dx = x - radius;
        var dy = y - radius;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > radius) return null;

        var hue = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
        var saturation = Math.Min(1.0, distance / radius);
        var (r, g, b) = FromHsv(hue, saturation, value);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>Where on the wheel a colour sits, for placing the marker.</summary>
    public static Point Locate(Color colour, double size)
    {
        var (hue, saturation, _) = ToHsv(colour);
        var radius = size / 2.0;
        var angle = hue * Math.PI / 180.0;
        return new Point(
            radius + Math.Cos(angle) * saturation * radius,
            radius + Math.Sin(angle) * saturation * radius);
    }

    public static (byte R, byte G, byte B) FromHsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60.0 % 2 - 1));
        var m = value - c;

        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return ((byte)Math.Clamp((r + m) * 255, 0, 255),
                (byte)Math.Clamp((g + m) * 255, 0, 255),
                (byte)Math.Clamp((b + m) * 255, 0, 255));
    }

    public static (double Hue, double Saturation, double Value) ToHsv(Color colour)
    {
        double r = colour.R / 255.0, g = colour.G / 255.0, b = colour.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var hue = delta == 0 ? 0
            : max == r ? 60 * (((g - b) / delta + 6) % 6)
            : max == g ? 60 * ((b - r) / delta + 2)
            : 60 * ((r - g) / delta + 4);

        return (hue, max == 0 ? 0 : delta / max, max);
    }
}
