using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Composites a CustomSprites st# skin folder into a static idle-pose preview using the
/// rig transforms captured from the live game (see SkinRigDefinition). Left/right facing is a
/// pure mirror of the same art (position, rotation, and bitmap all flip together) — the game's
/// "*Back" head/eye sprites are for a separate camera-facing state, not left/right movement, so
/// they are never used here.
/// </summary>
public static class SkinPreviewComposer
{
    /// <summary>
    /// Head shape -> CustomSprites file name (without extension). The plain head is
    /// experimentHeadBack: it is the blank head the game assigns to FacialExpression.defaultHead,
    /// with the eye drawn separately on top. (experimentHead has an eye baked into it and is unused,
    /// which is why some skins do not ship it at all.)
    /// </summary>
    private static string ResolveHeadFile(SkinHeadShape shape) => shape switch
    {
        SkinHeadShape.Normal => "experimentHeadBack",
        SkinHeadShape.NormalMouthOpen => "experimentHeadBackMouth",
        SkinHeadShape.NormalMouthHalf => "experimentHeadBackMouthMini",
        SkinHeadShape.Disfigured1 => "experimentHeadDisfigured1",
        SkinHeadShape.Disfigured1Healed => "experimentHeadDisfigured1Healed",
        SkinHeadShape.Disfigured2 => "experimentHeadDisfigured2",
        SkinHeadShape.Disfigured2Healed => "experimentHeadDisfigured2Healed",
        SkinHeadShape.Disfigured3 => "experimentHeadDisfigured3",
        SkinHeadShape.Disfigured3Healed => "experimentHeadDisfigured3Healed",
        _ => "experimentHead",
    };

    /// <summary>Eye expression -> CustomSprites file name (without extension).</summary>
    private static string? ResolveEyeFile(SkinEyeExpression expression) => expression switch
    {
        SkinEyeExpression.Open => "experimentEyeOpen",
        SkinEyeExpression.Closed => "experimentEyeClosed",
        SkinEyeExpression.HalfClosed => "experimentEyeHalfClosed",
        SkinEyeExpression.Happy => "experimentEyeHappy",
        SkinEyeExpression.Sad => "experimentEyeSad",
        SkinEyeExpression.Scared => "experimentEyeScared",
        SkinEyeExpression.Panic => "experimentEyePanic",
        SkinEyeExpression.Gone => "experimentEyeGone",
        SkinEyeExpression.GoneHealed => "experimentEyeGoneHealed",
        _ => null,
    };

    /// <summary>Eye expressions available in the dropdown — the same set regardless of facing, since facing is just a mirror of this same art.</summary>
    public static IReadOnlyList<SkinEyeExpression> AvailableEyeExpressions() =>
        [SkinEyeExpression.None, SkinEyeExpression.Open, SkinEyeExpression.Closed, SkinEyeExpression.HalfClosed, SkinEyeExpression.Happy,
         SkinEyeExpression.Sad, SkinEyeExpression.Scared, SkinEyeExpression.Panic, SkinEyeExpression.Gone, SkinEyeExpression.GoneHealed];

    /// <summary>
    /// Finds a sprite by file name, checking Head\ then Body\. The game's own loader reads both
    /// folders into a single dictionary keyed only by file name, so which of the two a sprite sits
    /// in makes no difference in game. Returns null when neither folder has it.
    /// </summary>
    private static string? ResolveSpritePath(string slotFolderPath, string spriteName)
    {
        foreach (var folder in (string[])["Head", "Body"])
        {
            var path = Path.Combine(slotFolderPath, folder, spriteName + ".png");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    /// <summary>
    /// The sprites a skin must ship for the default forward pose to render completely.
    /// Optional extras (disfigured heads, other eye states, nosebleed) are left out deliberately:
    /// plenty of complete skins omit them, so missing ones are not an install fault. experimentHead
    /// is also excluded, because the game never uses it.
    /// </summary>
    public static IReadOnlyList<string> RequiredSprites { get; } =
    [
        "experimentHeadBack",
        "experimentEyeOpen",
        "experimentUpTorso",
        "experimentDownTorso",
        "experimentUpArm",
        "experimentDownArm",
        "experimentHandF",
        "experimentHandB",
        "experimentThigh",
        "experimentCrus",
        "experimentFoot",
        "experimentTail",
    ];

    /// <summary>Names of the required sprites a slot does not contain, in the order listed above.</summary>
    public static List<string> FindMissingRequiredSprites(string slotFolderPath) =>
        RequiredSprites
            .Where(spriteName => ResolveSpritePath(slotFolderPath, spriteName) is null)
            .Select(spriteName => spriteName + ".png")
            .ToList();

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Builds the composed preview at native pixel scale (8px = 1 Unity unit). Wrap the result in a
    /// Viewbox to scale it up for display — the rig is tiny (character is roughly 45px tall natively).
    /// </summary>
    public static Canvas Compose(string slotFolderPath, SkinHeadShape headShape, SkinEyeExpression eyeExpression, bool facingBack)
    {
        var worldTransforms = SkinRigDefinition.ComputeWorldTransforms();

        var placements = new List<(BitmapImage Bitmap, SkinRigDefinition.WorldTransform World, int SortingOrder)>();

        foreach (var node in SkinRigDefinition.Nodes)
        {
            string? spriteName = node.Name switch
            {
                SkinRigDefinition.HeadNode => ResolveHeadFile(headShape),
                SkinRigDefinition.EyesNode => ResolveEyeFile(eyeExpression),
                _ => node.FixedSpriteName,
            };
            if (spriteName is null) continue;

            var path = ResolveSpritePath(slotFolderPath, spriteName);
            if (path is null) continue;

            var world = worldTransforms[node.Name];
            if (facingBack)
            {
                // Facing left is a pure mirror of the whole rig about its own vertical axis (X -> -X).
                // That's a reflection, which reverses rotational handedness, so the rotation angle must
                // negate too — otherwise limbs would end up bent the wrong way relative to the mirrored
                // torso. Every part's own bitmap flips along with it (applied below) — there is no
                // separate "back" art for this; it's the same sprite mirrored.
                world = world with { X = -world.X, RotationDeg = -world.RotationDeg };
            }

            placements.Add((LoadBitmap(path), world, node.SortingOrder));
        }

        const double ppu = SkinRigDefinition.PixelsPerUnit;

        // First pass: measure the exact pixel bounds. Each sprite's rotated axis-aligned extent is
        // computed properly rather than using a circular radius, so the canvas hugs the artwork and
        // the character sits centred in it instead of inside a much larger padded box.
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var (bitmap, world, _) in placements)
        {
            var centerX = world.X * ppu;
            var centerY = -world.Y * ppu;

            var radians = world.RotationDeg * Math.PI / 180.0;
            var cos = Math.Abs(Math.Cos(radians));
            var sin = Math.Abs(Math.Sin(radians));
            var halfWidth = bitmap.PixelWidth * Math.Abs(world.ScaleX) / 2.0;
            var halfHeight = bitmap.PixelHeight * Math.Abs(world.ScaleY) / 2.0;
            var extentX = halfWidth * cos + halfHeight * sin;
            var extentY = halfWidth * sin + halfHeight * cos;

            minX = Math.Min(minX, centerX - extentX);
            maxX = Math.Max(maxX, centerX + extentX);
            minY = Math.Min(minY, centerY - extentY);
            maxY = Math.Max(maxY, centerY + extentY);
        }
        if (placements.Count == 0) { minX = maxX = minY = maxY = 0; }

        const double padding = 4;
        var originX = -minX + padding;
        var originY = -minY + padding;
        var canvas = new Canvas
        {
            Width = maxX - minX + padding * 2,
            Height = maxY - minY + padding * 2,
            Background = Brushes.Transparent,
            SnapsToDevicePixels = true,
        };
        RenderOptions.SetBitmapScalingMode(canvas, BitmapScalingMode.NearestNeighbor);

        foreach (var (bitmap, world, sortingOrder) in placements)
        {
            var image = new Image
            {
                Source = bitmap,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight,
                Stretch = Stretch.None,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(facingBack ? -world.ScaleX : world.ScaleX, world.ScaleY),
                        new RotateTransform(-world.RotationDeg),
                    },
                },
            };

            var centerX = originX + world.X * ppu;
            var centerY = originY - world.Y * ppu;
            Canvas.SetLeft(image, centerX - bitmap.PixelWidth / 2.0);
            Canvas.SetTop(image, centerY - bitmap.PixelHeight / 2.0);
            Panel.SetZIndex(image, sortingOrder);

            canvas.Children.Add(image);
        }

        return canvas;
    }
}
