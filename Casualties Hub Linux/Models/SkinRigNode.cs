namespace Casualties_Hub.Models;

/// <summary>
/// One SpriteRenderer-bearing part of the Experiment ragdoll, captured live from the
/// running game via UnityExplorer (Experiment/Body hierarchy). Position/rotation/scale
/// are local to ParentName ("" means parented directly to Body's own origin).
/// </summary>
public class SkinRigNode
{
    public required string Name { get; init; }
    public string ParentName { get; init; } = "";
    public double LocalX { get; init; }
    public double LocalY { get; init; }
    public double LocalRotationDeg { get; init; }
    public double LocalScaleX { get; init; } = 1;
    public double LocalScaleY { get; init; } = 1;

    /// <summary>SpriteRenderer.sortingOrder captured in UnityExplorer. Maps directly to Panel.ZIndex.</summary>
    public int SortingOrder { get; init; }

    /// <summary>Fixed CustomSprites file name (without extension) for parts whose art never changes with skin state. Null for Head/Eyes, whose art is chosen by the selected head shape / expression.</summary>
    public string? FixedSpriteName { get; init; }
}

public enum SkinHeadShape
{
    Normal,
    NormalMouthOpen,
    NormalMouthHalf,
    Disfigured1,
    Disfigured1Healed,
    Disfigured2,
    Disfigured2Healed,
    Disfigured3,
    Disfigured3Healed,
}

public enum SkinEyeExpression
{
    None,
    Open,
    Closed,
    HalfClosed,
    Happy,
    Sad,
    Scared,
    Panic,
    Gone,
    GoneHealed,
}
