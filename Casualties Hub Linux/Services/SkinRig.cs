using System.IO;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// The toolkit-independent half of skin preview: which sprite file backs a given head shape or
/// eye expression, where to find it on disk, and which sprites a slot is missing. Drawing the
/// preview itself lives in SkinPreviewComposer.
/// </summary>
public static class SkinRig
{
    /// <summary>
    /// Head shape -> CustomSprites file name (without extension). The plain head is
    /// experimentHeadBack: it is the blank head the game assigns to FacialExpression.defaultHead,
    /// with the eye drawn separately on top. (experimentHead has an eye baked into it and is unused,
    /// which is why some skins do not ship it at all.)
    /// </summary>
    public static string ResolveHeadFile(SkinHeadShape shape) => shape switch
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
    public static string? ResolveEyeFile(SkinEyeExpression expression) => expression switch
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
    public static string? ResolveSpritePath(string slotFolderPath, string spriteName)
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
}
