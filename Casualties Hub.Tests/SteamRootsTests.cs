using Casualties_Hub.Services;
using Xunit;

namespace Casualties_Hub.Tests;

/// <summary>
/// Steam root discovery is the one place detection differs by platform. These pin down that
/// each platform only ever looks in its own locations.
/// </summary>
public sealed class SteamRootsTests
{
    [Fact]
    public void CandidateRootsAreNeverEmpty()
    {
        Assert.NotEmpty(SteamLibraryLocator.CandidateRoots());
    }

    [Fact]
    public void CandidateRootsMatchThePlatform()
    {
        var roots = SteamLibraryLocator.CandidateRoots();

        if (OperatingSystem.IsWindows())
        {
            Assert.All(roots, root => Assert.True(Path.IsPathRooted(root), root));
            Assert.Contains(roots, root => root.EndsWith(@"\Steam", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(roots, root => root.StartsWith("/usr", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains("/usr/games/Steam", roots);
            Assert.DoesNotContain(roots, root => root.Contains("Program Files", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void RegistryLookupIsWindowsOnly()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Null(SteamLibraryLocator.WindowsSteamRoot());
        else if (SteamLibraryLocator.WindowsSteamRoot() is { } root)
            Assert.True(Path.IsPathRooted(root), root);
    }
}
