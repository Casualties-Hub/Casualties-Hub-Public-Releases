using Casualties_Hub.Services;
using Xunit;

namespace Casualties_Hub.Tests;

/// <summary>
/// Case handling and containment. These back the two behaviours that decide whether the Linux
/// build works at all: finding folders whose casing differs, and refusing to treat a path outside
/// a container as inside it.
/// </summary>
public sealed class LinuxPathsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("chpaths").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>
    /// True when the test is running on a case-sensitive filesystem.
    /// </summary>
    /// <remarks>
    /// These tests describe Linux behaviour but the development machine is Windows, where NTFS
    /// resolves "Plugins" to a folder named "plugins" by itself. On NTFS the exact-match fast path
    /// succeeds and returns the requested casing; on ext4 the enumeration fallback runs and returns
    /// the real on-disk name. Asserting one fixed answer would make the suite fail on whichever
    /// platform it was not written for, so the expected casing is chosen from the filesystem.
    /// </remarks>
    private bool IsCaseSensitive
    {
        get
        {
            var probe = Path.Combine(_root, $".case-{Guid.NewGuid():N}");
            Directory.CreateDirectory(probe);
            try
            {
                File.WriteAllText(Path.Combine(probe, "Aa"), "probe");
                return !File.Exists(Path.Combine(probe, "aA"));
            }
            finally
            {
                Directory.Delete(probe, recursive: true);
            }
        }
    }

    [Fact]
    public void FindChild_matches_regardless_of_case()
    {
        // BepInEx under Proton commonly creates lowercase "plugins" while the Hub asks for "Plugins".
        Directory.CreateDirectory(Path.Combine(_root, "plugins"));

        var found = LinuxPaths.FindChild(_root, "Plugins");

        Assert.NotNull(found);
        Assert.True(Directory.Exists(found));
        // The point being proven is that the lookup resolves at all; only a case-sensitive
        // filesystem can show the fallback returning the real on-disk name.
        if (IsCaseSensitive) Assert.Equal("plugins", Path.GetFileName(found));
    }

    [Fact]
    public void FindChild_prefers_the_exact_match_when_both_exist()
    {
        Directory.CreateDirectory(Path.Combine(_root, "plugins"));
        Directory.CreateDirectory(Path.Combine(_root, "Plugins"));

        Assert.Equal("Plugins", Path.GetFileName(LinuxPaths.FindChild(_root, "Plugins")));
    }

    [Fact]
    public void FindChild_returns_null_when_absent()
    {
        Assert.Null(LinuxPaths.FindChild(_root, "Plugins"));
        Assert.Null(LinuxPaths.FindChild(Path.Combine(_root, "nope"), "Plugins"));
    }

    [Fact]
    public void ResolveChild_falls_back_to_exact_case_for_creation()
    {
        var resolved = LinuxPaths.ResolveChild(_root, "BepInEx");

        Assert.Equal(Path.Combine(_root, "BepInEx"), resolved);
    }

    [Fact]
    public void ResolveChain_walks_every_level_case_insensitively()
    {
        Directory.CreateDirectory(Path.Combine(_root, "bepinex", "PLUGINS"));

        var resolved = LinuxPaths.ResolveChain(_root, "BepInEx", "Plugins");

        // Resolving through two mismatched segments is the real requirement here.
        Assert.True(Directory.Exists(resolved));
        if (IsCaseSensitive) Assert.Equal("PLUGINS", Path.GetFileName(resolved));
    }

    [Theory]
    [InlineData("/games/plugins/mod.dll", "/games/plugins", true)]
    [InlineData("/games/plugins", "/games/plugins", true)]
    [InlineData("/games/plugins-evil/mod.dll", "/games/plugins", false)]
    [InlineData("/games/other/mod.dll", "/games/plugins", false)]
    public void IsInside_only_accepts_real_descendants(string candidate, string container, bool expected)
    {
        // "plugins-evil" is the case a naive StartsWith gets wrong, and this guard stands in
        // front of recursive deletes.
        Assert.Equal(expected, LinuxPaths.IsInside(candidate, container));
    }

    [Fact]
    public void IsInside_is_case_sensitive()
    {
        // On ext4 these are two different directories, so treating them as one would let a
        // containment check pass for a path it should reject.
        Assert.False(LinuxPaths.IsInside("/games/Plugins/mod.dll", "/games/plugins"));
    }

    [Fact]
    public void IsInside_rejects_traversal_out_of_the_container()
    {
        Assert.False(LinuxPaths.IsInside("/games/plugins/../../etc/passwd", "/games/plugins"));
    }

    [Fact]
    public void AppDataRoot_is_always_absolute()
    {
        // GetFolderPath returns "" on Unix when the directory does not exist, which would make
        // this relative and scatter settings into the working directory.
        var root = LinuxPaths.AppDataRoot();

        Assert.True(Path.IsPathRooted(root), $"AppDataRoot returned a relative path: '{root}'");
        Assert.EndsWith("CasualtiesHub", root);
    }
}
