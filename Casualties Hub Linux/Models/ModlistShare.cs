namespace Casualties_Hub.Models;

public sealed class ModlistShare
{
    public int FormatVersion { get; init; } = 1;
    public List<ModlistEntry> Mods { get; init; } = [];
}

public sealed class ModlistEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "Unknown mod";
    public string Guid { get; init; } = "";
    public string? Version { get; init; }
}
