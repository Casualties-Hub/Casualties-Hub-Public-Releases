namespace Casualties_Hub_Installer.Models;

public sealed record HubRelease(
    Version Version,
    string Tag,
    bool IsPrerelease,
    string ReleasePageUrl,
    string PackageUrl,
    string PackageName,
    string? Sha256)
{
    public override string ToString() => IsPrerelease ? $"{Tag} (Pre-Release)" : Tag;
}
