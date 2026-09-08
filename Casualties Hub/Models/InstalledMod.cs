namespace Casualties_Hub.Models;

public sealed class InstalledMod
{
    public string Name { get; init; } = "Unknown mod";
    public string? InstalledVersion { get; init; }
    public string? ExpectedVersion { get; init; }
    public string? AssemblyVersion { get; init; }
    public string? BepInExVersion { get; init; }
    public DateTimeOffset? FileModifiedUtc { get; init; }
    public DateTimeOffset? ExpectedModifiedUtc { get; init; }
    public bool IsUpToDate { get; init; }
    public bool IsOutOfDate { get; init; }
    public bool IsDisabled { get; init; }
    public string? MetadataId { get; init; }
    public string? ModGuid { get; init; }
    public string? NexusUrl { get; init; }
    public IReadOnlyList<string> PluginDllPaths { get; init; } = [];
    public string SourceEntryPath { get; init; } = "";
    public IReadOnlyList<DependencyRequirement> RequiredDependencies { get; init; } = [];
    public IReadOnlyList<DependencyRequirement> MissingDependencies { get; set; } = [];
    public IReadOnlyList<string> IncompatibleWith { get; set; } = [];
    public IReadOnlyList<string> KnownBugs { get; set; } = [];
    public bool IsDependencyPlaceholder { get; init; }
    public string? DependencyRequiredByLabel { get; init; }
    public MetadataMod? DependencyMetadata { get; init; }
    public string DependencyActionLabel { get; init; } = "Open Download";
    public string ShareCodeActionLabel { get; init; } = "Open Download";
    public bool IsRequestedByModlist { get; set; }
    public bool IsMissingFromModlist { get; set; }
    public string Description { get; init; } = "No description was supplied by the mod author.";

    public string VersionLabel => IsDependencyPlaceholder
        ? "Not installed dependency"
        : IsMissingFromModlist
        ? "Not installed — requested by modlist"
        : string.IsNullOrWhiteSpace(InstalledVersion)
        ? "Version could not be read"
        : $"Installed: {InstalledVersion}";

    public string VersionEvidenceLabel
    {
        get
        {
            var evidence = new List<string>();
            if (!string.IsNullOrWhiteSpace(AssemblyVersion)) evidence.Add($"Assembly {AssemblyVersion}");
            if (!string.IsNullOrWhiteSpace(BepInExVersion)) evidence.Add($"BepInEx {BepInExVersion}");
            if (FileModifiedUtc is not null) evidence.Add($"Modified {FileModifiedUtc.Value.LocalDateTime:g}");
            return evidence.Count == 0 ? "No version evidence found" : string.Join(" | ", evidence);
        }
    }

    public string UpdateStatusLabel { get; init; } = "No comparable update data was found.";
    public string ToggleButtonLabel => IsDisabled ? "Enable" : "Disable";
    public bool HasRequiredDependencies => RequiredDependencies.Count > 0;
    public string RequiredDependenciesLabel => "Requires: " + string.Join(", ", RequiredDependencies.Select(dependency => dependency.DisplayLabel));
    public bool HasMissingDependencies => MissingDependencies.Count > 0;
    public string MissingDependenciesLabel => "Missing: " + string.Join(", ", MissingDependencies.Select(dependency => dependency.DisplayLabel)) + ". Please install before launching.";
    public bool HasIncompatibilities => IncompatibleWith.Count > 0;
    public string IncompatibilityLabel => string.Join(" | ", IncompatibleWith.Select(mod => $"Incompatible with mod {mod}"));
    public bool HasKnownBugs => KnownBugs.Count > 0;
    public string KnownBugsLabel => string.Join(" | ", KnownBugs.Select(message => $"Known bug: {message}"));
}
