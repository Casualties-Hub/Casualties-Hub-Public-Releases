using Casualties_Hub.Models;
using Casualties_Hub.Services;
using Xunit;

namespace Casualties_Hub.Tests;

/// <summary>
/// Mods that are not in the community catalogue.
/// </summary>
/// <remarks>
/// Regression cover for a bug a Linux tester hit: pressing Enable/Disable threw
/// "This mod has no managed DLL file to enable or disable". CreateInstalledMod returned a
/// name-only stub whenever no metadata entry matched, leaving PluginDllPaths and SourceEntryPath
/// empty and IsDisabled false, so toggling and deleting both failed and a disabled mod displayed
/// as enabled. Community mods routinely predate the catalogue, so this is the ordinary case
/// rather than an edge case, and every one of these assertions failed before the fix.
/// </remarks>
public sealed class UnmatchedModTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("chunmatched").FullName;
    private readonly ModService _modService = new();

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>A game folder with the given files dropped into BepInEx/plugins.</summary>
    private Settings GameWith(params string[] pluginFileNames)
    {
        var plugins = Path.Combine(_root, "game", "BepInEx", "plugins");
        Directory.CreateDirectory(plugins);
        foreach (var name in pluginFileNames)
        {
            var path = Path.Combine(plugins, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "not a real assembly");
        }
        return new Settings { GamePath = Path.Combine(_root, "game") };
    }

    private InstalledMod Single(Settings settings)
    {
        // Empty metadata is the whole point: this is what an uncatalogued mod looks like.
        var mods = _modService.GetInstalledModsWithMetadata(settings, []);
        return Assert.Single(mods);
    }

    [Fact]
    public void An_uncatalogued_mod_can_still_be_toggled()
    {
        var mod = Single(GameWith("CommunityMod.dll"));

        Assert.NotEmpty(mod.PluginDllPaths);
    }

    [Fact]
    public void An_uncatalogued_mod_can_still_be_deleted()
    {
        var mod = Single(GameWith("CommunityMod.dll"));

        Assert.False(string.IsNullOrWhiteSpace(mod.SourceEntryPath));
        Assert.True(File.Exists(mod.SourceEntryPath) || Directory.Exists(mod.SourceEntryPath));
    }

    [Fact]
    public void A_disabled_uncatalogued_mod_reports_itself_as_disabled()
    {
        // Previously defaulted to false, so the button offered "Disable" on an already-disabled mod.
        var mod = Single(GameWith("CommunityMod.dll.disabled"));

        Assert.True(mod.IsDisabled);
        Assert.Equal("Enable", mod.ToggleButtonLabel);
    }

    [Fact]
    public void An_enabled_uncatalogued_mod_reports_itself_as_enabled()
    {
        var mod = Single(GameWith("CommunityMod.dll"));

        Assert.False(mod.IsDisabled);
        Assert.Equal("Disable", mod.ToggleButtonLabel);
    }

    [Fact]
    public void Toggling_an_uncatalogued_mod_actually_renames_the_file()
    {
        var settings = GameWith("CommunityMod.dll");
        var plugins = _modService.GetPluginsPath(settings);

        _modService.ToggleModDisabled(Single(settings));

        Assert.False(File.Exists(Path.Combine(plugins, "CommunityMod.dll")));
        Assert.True(File.Exists(Path.Combine(plugins, "CommunityMod.dll.disabled")));
    }

    [Fact]
    public void Toggling_back_re_enables_an_uncatalogued_mod()
    {
        var settings = GameWith("CommunityMod.dll.disabled");
        var plugins = _modService.GetPluginsPath(settings);

        _modService.ToggleModDisabled(Single(settings));

        Assert.True(File.Exists(Path.Combine(plugins, "CommunityMod.dll")));
        Assert.False(File.Exists(Path.Combine(plugins, "CommunityMod.dll.disabled")));
    }

    [Fact]
    public void Deleting_an_uncatalogued_mod_removes_it()
    {
        var settings = GameWith("CommunityMod.dll");
        var plugins = _modService.GetPluginsPath(settings);

        _modService.DeleteInstalledMod(Single(settings));

        Assert.False(File.Exists(Path.Combine(plugins, "CommunityMod.dll")));
    }

    [Fact]
    public void A_folder_mod_collects_every_dll_inside_it()
    {
        var settings = GameWith(Path.Combine("BigMod", "Main.dll"), Path.Combine("BigMod", "lib", "Helper.dll"));

        var mod = Single(settings);

        Assert.Equal(2, mod.PluginDllPaths.Count);
        Assert.True(Directory.Exists(mod.SourceEntryPath));
    }

    [Fact]
    public void An_uppercase_extension_is_still_recognised()
    {
        // Case-sensitive filesystems make this the difference between a listed mod and a
        // silently missing one.
        var mod = Single(GameWith("ExtraGuns.DLL"));

        Assert.NotEmpty(mod.PluginDllPaths);
        Assert.False(string.IsNullOrWhiteSpace(mod.SourceEntryPath));
    }

    [Fact]
    public void The_mod_is_named_after_its_file_rather_than_Unknown()
    {
        var mod = Single(GameWith("CommunityMod.dll"));

        Assert.DoesNotContain("Unknown", mod.Name);
        Assert.Contains("CommunityMod", mod.Name);
    }
}
