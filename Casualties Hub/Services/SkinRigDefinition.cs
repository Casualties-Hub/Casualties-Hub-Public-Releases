using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// The Experiment ragdoll's idle-pose rig, captured directly from the live game (UnityExplorer,
/// Experiment/Body hierarchy, 2026-08-03). All 15 SpriteRenderer limbs are parented flat to Body;
/// Eyes is a child of Head and Tail is a child of DownTorso, so those two need real parent-chain
/// composition. Units are Unity local units; the game's CustomSprites loader uses 8 pixels per unit
/// (see body_sprite_replacer.dll: Sprite.Create(..., pixelsPerUnit: 8f)).
/// </summary>
public static class SkinRigDefinition
{
    public const double PixelsPerUnit = 8.0;

    public const string HeadNode = "Head";
    public const string EyesNode = "Eyes";

    public static readonly IReadOnlyList<SkinRigNode> Nodes =
    [
        new() { Name = HeadNode, LocalX = 0.0414, LocalY = 2.4445, LocalRotationDeg = 357.9453, LocalScaleX = 1, SortingOrder = 50, FixedSpriteName = null },
        new() { Name = EyesNode, ParentName = HeadNode, LocalX = 0, LocalY = 0, LocalRotationDeg = 0, LocalScaleX = 1, SortingOrder = 53, FixedSpriteName = null },

        new() { Name = "UpTorso", LocalX = 0.0093, LocalY = 1.1965, LocalRotationDeg = 359.4024, LocalScaleX = 1.057, SortingOrder = 20, FixedSpriteName = "experimentUpTorso" },
        new() { Name = "DownTorso", LocalX = -0.0006, LocalY = 0.1974, LocalRotationDeg = 359.4426, LocalScaleX = 1.0951, SortingOrder = 10, FixedSpriteName = "experimentDownTorso" },
        new() { Name = "Tail", ParentName = "DownTorso", LocalX = 0, LocalY = -0.353, LocalRotationDeg = 24.7188, LocalScaleX = 0.9132, SortingOrder = 0, FixedSpriteName = "experimentTail" },

        new() { Name = "UpArmF", LocalX = -0.1946, LocalY = 0.8364, LocalRotationDeg = 350.305, LocalScaleX = 1.0808, SortingOrder = 150, FixedSpriteName = "experimentUpArm" },
        new() { Name = "DownArmF", LocalX = -0.1436, LocalY = -0.3912, LocalRotationDeg = 10.1996, LocalScaleX = 1.0523, SortingOrder = 160, FixedSpriteName = "experimentDownArm" },
        new() { Name = "HandF", LocalX = -0.0147, LocalY = -1.2496, LocalRotationDeg = 3.7128, LocalScaleX = 1, SortingOrder = 170, FixedSpriteName = "experimentHandF" },

        new() { Name = "UpArmB", LocalX = -0.0991, LocalY = 0.8229, LocalRotationDeg = 1.0709, LocalScaleX = 1.0808, SortingOrder = -50, FixedSpriteName = "experimentUpArm" },
        new() { Name = "DownArmB", LocalX = 0.0299, LocalY = -0.4119, LocalRotationDeg = 9.5132, LocalScaleX = 1.0523, SortingOrder = -60, FixedSpriteName = "experimentDownArm" },
        new() { Name = "HandB", LocalX = 0.2008, LocalY = -1.2542, LocalRotationDeg = 19.6041, LocalScaleX = 1, SortingOrder = -40, FixedSpriteName = "experimentHandB" },

        new() { Name = "ThighF", LocalX = -0.1782, LocalY = -0.616, LocalRotationDeg = 353.4347, LocalScaleX = 1.0808, SortingOrder = 100, FixedSpriteName = "experimentThigh" },
        new() { Name = "CrusF", LocalX = -0.5638, LocalY = -1.2648, LocalRotationDeg = 292.7933, LocalScaleX = 1.0713, SortingOrder = 90, FixedSpriteName = "experimentCrus" },
        new() { Name = "FootF", LocalX = -0.755, LocalY = -2.0296, LocalRotationDeg = 34.3507, LocalScaleX = 1.0238, SortingOrder = 80, FixedSpriteName = "experimentFoot" },

        new() { Name = "ThighB", LocalX = 0.0906, LocalY = -0.5643, LocalRotationDeg = 24.1302, LocalScaleX = 1.0808, SortingOrder = -10, FixedSpriteName = "experimentThigh" },
        new() { Name = "CrusB", LocalX = 0.0063, LocalY = -1.2471, LocalRotationDeg = 310.9312, LocalScaleX = 1.0713, SortingOrder = -20, FixedSpriteName = "experimentCrus" },
        new() { Name = "FootB", LocalX = 0.0026, LocalY = -2.061, LocalRotationDeg = 45.3621, LocalScaleX = 1.0238, SortingOrder = -30, FixedSpriteName = "experimentFoot" },
    ];

    public readonly record struct WorldTransform(double X, double Y, double RotationDeg, double ScaleX, double ScaleY);

    /// <summary>Resolves every node's transform in Body-local space by walking each node's parent chain (handles Eyes-under-Head and Tail-under-DownTorso).</summary>
    public static Dictionary<string, WorldTransform> ComputeWorldTransforms()
    {
        var byName = Nodes.ToDictionary(n => n.Name);
        var resolved = new Dictionary<string, WorldTransform>();

        WorldTransform Resolve(string name)
        {
            if (resolved.TryGetValue(name, out var cached)) return cached;
            var node = byName[name];
            if (string.IsNullOrEmpty(node.ParentName))
            {
                var root = new WorldTransform(node.LocalX, node.LocalY, node.LocalRotationDeg, node.LocalScaleX, node.LocalScaleY);
                resolved[name] = root;
                return root;
            }

            var parent = Resolve(node.ParentName);
            var parentRad = parent.RotationDeg * Math.PI / 180.0;
            var cos = Math.Cos(parentRad);
            var sin = Math.Sin(parentRad);

            // Unity TRS composition: scale local offset by parent scale, then rotate by parent rotation, then translate by parent position.
            var scaledX = node.LocalX * parent.ScaleX;
            var scaledY = node.LocalY * parent.ScaleY;
            var rotatedX = scaledX * cos - scaledY * sin;
            var rotatedY = scaledX * sin + scaledY * cos;

            var world = new WorldTransform(
                parent.X + rotatedX,
                parent.Y + rotatedY,
                (parent.RotationDeg + node.LocalRotationDeg) % 360.0,
                parent.ScaleX * node.LocalScaleX,
                parent.ScaleY * node.LocalScaleY);
            resolved[name] = world;
            return world;
        }

        foreach (var node in Nodes) Resolve(node.Name);
        return resolved;
    }
}
