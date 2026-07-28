namespace Casualties_Hub_Installer.Models;

/// <summary>One discovered Hub copy, with a temporary selection flag used only by the Setup Wizard UI.</summary>
public sealed class DetectedInstallation
{
    public required string Path { get; init; }
    public required string Version { get; init; }
    public required DateTimeOffset LastInstalledUtc { get; init; }
    public bool IsSelected { get; set; }
}
