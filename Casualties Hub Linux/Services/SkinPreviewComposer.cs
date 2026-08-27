using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Composites a CustomSprites st# skin folder into a static idle-pose preview using the rig
/// transforms captured from the live game (see SkinRigDefinition). Left/right facing is a pure
/// mirror of the same art — position, rotation and bitmap all flip together — so the game's
/// "*Back" head/eye sprites, which belong to a separate camera-facing state, are never used here.
/// </summary>
/// <remarks>
/// Sprite naming and lookup live in <see cref="SkinRig"/>; this only draws. Two details matter:
/// <list type="bullet">
/// <item>Interpolation must be BitmapInterpolationMode.None, or every skin renders as blurred
/// mush.</item>
/// <item>Sizes come from PixelSize, never from Bitmap.Size, which is DPI-derived: a 72-DPI sprite
/// would lay out ~33% too large.</item>
/// </list>
/// </remarks>
public static class SkinPreviewComposer
{
    /// <summary>Padding around the composed figure, in source pixels.</summary>
    private const double Padding = 4;

    /// <summary>
    /// Builds the preview at native pixel scale (8px = 1 Unity unit). Wrap the result in a
    /// Viewbox to enlarge it: the rig is tiny, roughly 45px tall.
    /// </summary>
    public static Canvas Compose(string slotFolderPath, SkinHeadShape headShape, SkinEyeExpression eyeExpression, bool facingBack)
    {
        var worldTransforms = SkinRigDefinition.ComputeWorldTransforms();
        var placements = new List<(Bitmap Bitmap, SkinRigDefinition.WorldTransform World, int SortingOrder)>();

        foreach (var node in SkinRigDefinition.Nodes)
        {
            string? spriteName = node.Name switch
            {
                SkinRigDefinition.HeadNode => SkinRig.ResolveHeadFile(headShape),
                SkinRigDefinition.EyesNode => SkinRig.ResolveEyeFile(eyeExpression),
                _ => node.FixedSpriteName,
            };
            if (spriteName is null) continue;

            var path = SkinRig.ResolveSpritePath(slotFolderPath, spriteName);
            if (path is null) continue;

            Bitmap bitmap;
            try
            {
                bitmap = new Bitmap(path);
            }
            catch (Exception exception)
            {
                // A corrupt or truncated PNG must not take down the whole preview; the missing
                // part simply does not draw, and the missing-sprite banner already reports it.
                DebugLogService.Error($"Could not load sprite {System.IO.Path.GetFileName(path)}", exception);
                continue;
            }

            var world = worldTransforms[node.Name];
            if (facingBack)
            {
                // Facing left mirrors the whole rig about its vertical axis (X -> -X). That is a
                // reflection, which reverses rotational handedness, so the angle negates too;
                // otherwise limbs bend the wrong way relative to the mirrored torso.
                world = world with { X = -world.X, RotationDeg = -world.RotationDeg };
            }

            placements.Add((bitmap, world, node.SortingOrder));
        }

        const double ppu = SkinRigDefinition.PixelsPerUnit;

        // First pass: measure exact bounds. Each sprite's rotated axis-aligned extent is computed
        // properly rather than with a circular radius, so the canvas hugs the artwork and the
        // figure sits centred instead of floating in a padded box.
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var (bitmap, world, _) in placements)
        {
            var centerX = world.X * ppu;
            var centerY = -world.Y * ppu;

            var radians = world.RotationDeg * Math.PI / 180.0;
            var cos = Math.Abs(Math.Cos(radians));
            var sin = Math.Abs(Math.Sin(radians));
            var halfWidth = bitmap.PixelSize.Width * Math.Abs(world.ScaleX) / 2.0;
            var halfHeight = bitmap.PixelSize.Height * Math.Abs(world.ScaleY) / 2.0;

            minX = Math.Min(minX, centerX - (halfWidth * cos + halfHeight * sin));
            maxX = Math.Max(maxX, centerX + (halfWidth * cos + halfHeight * sin));
            minY = Math.Min(minY, centerY - (halfWidth * sin + halfHeight * cos));
            maxY = Math.Max(maxY, centerY + (halfWidth * sin + halfHeight * cos));
        }
        if (placements.Count == 0) { minX = maxX = minY = maxY = 0; }

        var originX = -minX + Padding;
        var originY = -minY + Padding;

        var canvas = new Canvas
        {
            Width = maxX - minX + Padding * 2,
            Height = maxY - minY + Padding * 2,
            Background = Brushes.Transparent,
        };

        // Without this the Viewbox scales the pixel art with smoothing and the result is mush.
        RenderOptions.SetBitmapInterpolationMode(canvas, BitmapInterpolationMode.None);

        foreach (var (bitmap, world, sortingOrder) in placements)
        {
            var width = bitmap.PixelSize.Width;
            var height = bitmap.PixelSize.Height;

            var image = new Image
            {
                Source = bitmap,
                // Explicit pixel dimensions with Fill, rather than Stretch.None: Bitmap.Size is
                // DPI-derived, so a sprite saved at 72 DPI would otherwise draw a third too big.
                Width = width,
                Height = height,
                Stretch = Stretch.Fill,
                RenderTransformOrigin = RelativePoint.Center,
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(facingBack ? -world.ScaleX : world.ScaleX, world.ScaleY),
                        new RotateTransform(-world.RotationDeg),
                    },
                },
                ZIndex = sortingOrder,
            };

            // Also set per-image, not just on the canvas. The Skins page scales the whole preview
            // through a Viewbox, and the attached mode is read from the element being drawn, so
            // relying on inheritance alone leaves the upscaled sprites smoothed into mush.
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);

            Canvas.SetLeft(image, originX + world.X * ppu - width / 2.0);
            Canvas.SetTop(image, originY - world.Y * ppu - height / 2.0);

            canvas.Children.Add(image);
        }

        return canvas;
    }
}
