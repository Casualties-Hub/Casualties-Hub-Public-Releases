namespace Casualties_Hub.Models;

public enum ArchiveInstallKind
{
    PluginDll,
    BepInExLayout,
    CustomSprite,
    Unsupported
}

public sealed class ArchiveInstallPlan
{
    public ArchiveInstallKind Kind { get; init; }
    public IReadOnlyList<string> DllNames { get; init; } = [];
    public IReadOnlyList<string> MatchingModNames { get; init; } = [];
    public IReadOnlyList<string> ExistingFilesToReplace { get; init; } = [];
    public IReadOnlyList<DependencyRequirement> KnownDependencies { get; init; } = [];
    public bool NeedsManualDependencyReview { get; init; }
    public bool RequiresSkinSlot => Kind == ArchiveInstallKind.CustomSprite;

    public string DependencyPrompt
    {
        get
        {
            var lines = new List<string>();
            if (KnownDependencies.Count > 0)
            {
                lines.Add("Known required library/libraries:");
                lines.AddRange(KnownDependencies.Select(dependency => $"• {dependency.DisplayLabel}"));
                lines.Add("Install required libraries before launching the game. This alpha does not download dependencies automatically.");
            }

            if (NeedsManualDependencyReview)
            {
                if (lines.Count > 0) lines.Add("");
                lines.Add("Dependency list not verified for this mod. Read the mod's Nexus description and Files page for any additional requirements before installing.");
            }

            return lines.Count == 0 ? "" : "\n\n" + string.Join("\n", lines);
        }
    }

    public string Description => Kind switch
    {
        ArchiveInstallKind.PluginDll => "DLL-only mod: DLL files will be installed in BepInEx\\Plugins.",
        ArchiveInstallKind.BepInExLayout => "BepInEx/Plugins layout detected: incoming files will merge into the existing BepInEx installation without deleting BepInEx itself.",
        ArchiveInstallKind.CustomSprite => "Custom sprite detected: choose the CustomSprites st0-st9 slot to replace.",
        _ => "No supported DLL, BepInEx/Plugins layout, or experimentCrus.png sprite layout was found."
    };
}
