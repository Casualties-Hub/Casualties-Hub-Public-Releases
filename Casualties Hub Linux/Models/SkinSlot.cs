namespace Casualties_Hub.Models;

/// <summary>One CustomSprites st# folder found in the game's Plugins folder.</summary>
public class SkinSlot
{
    public required string Name { get; init; }
    /// <summary>The numeric part of the slot name, used so st10 sorts after st9 rather than after st1.</summary>
    public required int Number { get; init; }
    public required string FolderPath { get; init; }
    public int HeadSpriteCount { get; init; }
    public int BodySpriteCount { get; init; }

    /// <summary>Required sprites this slot does not contain. A non-empty list means the install is incomplete.</summary>
    public IReadOnlyList<string> MissingSprites { get; init; } = [];

    public int SpriteCount => HeadSpriteCount + BodySpriteCount;
    public bool IsIncomplete => MissingSprites.Count > 0;

    /// <summary>Shown in the slot list: the slot name plus what art it actually contains.</summary>
    public string DisplayLabel => $"{Name.ToUpperInvariant()}   ({HeadSpriteCount} head, {BodySpriteCount} body)";

    /// <summary>Hover text. Complete slots get no tooltip so only problem slots draw attention.</summary>
    public string? StatusTooltip => IsIncomplete
        ? $"Incorrect or missing textures, re-install.\n\nNot found in this slot:\n{string.Join("\n", MissingSprites)}"
        : null;

    // Added for the Avalonia skins list. Avalonia has no DataTriggers, so a template binds
    // IsVisible to a bool and Text to a ready-made string rather than deriving either in markup.
    public string SpriteSummary => $"{HeadSpriteCount} head · {BodySpriteCount} body";

    public bool HasMissing => IsIncomplete;

    public string MissingSummary => IsIncomplete
        ? $"Missing {MissingSprites.Count} required sprites: {string.Join(", ", MissingSprites)}"
        : "";
}
