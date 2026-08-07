namespace Casualties_Hub.Models;

/// <summary>
/// A single row in the Multiplayer server browser preview. Nothing populates
/// this from a network service yet; the page builds sample rows so the layout
/// can be reviewed ahead of a real backend.
/// </summary>
public sealed class MultiplayerServer
{
    public string Name { get; init; } = "";
    public string RegionCode { get; init; } = "";
    public int Layer { get; init; }
    public string Mood { get; init; } = "";
    public bool Locked { get; init; }
    public int Population { get; init; }
    public int Capacity { get; init; }
    public int Ping { get; init; }
    public int ModCount { get; init; }

    public string LayerLabel => $"Layer {Layer}";
    public string LockLabel => Locked ? "Locked" : "";
    public string PopulationLabel => $"{Population}/{Capacity}";
    public string PingLabel => $"{Ping}ms";
}
