namespace Casualties_Hub.Models;

/// <summary>A library a mod author has identified as required by their mod.</summary>
public sealed record DependencyRequirement(string Name, string? MinimumVersion = null, string? Note = null)
{
    public string DisplayLabel
    {
        get
        {
            var version = string.IsNullOrWhiteSpace(MinimumVersion) ? "" : $" {MinimumVersion}+";
            var note = string.IsNullOrWhiteSpace(Note) ? "" : $" ({Note})";
            return $"{Name}{version}{note}";
        }
    }
}
