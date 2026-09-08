namespace Casualties_Hub.Models;

/// <summary>One independently removable piece of Hub data, offered as a checklist entry in the Uninstall dialog.</summary>
public class UninstallItem
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> Paths { get; init; }
    public bool IsSelected { get; set; } = true;
}
