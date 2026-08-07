using Casualties_Hub.Models;
using Casualties_Hub.Services;
using Xunit;

namespace Casualties_Hub.Tests;

/// <summary>
/// Backup and restore. Restore overwrites files inside a real game install, so it is the one
/// operation in this build with the potential to destroy a user's mods.
/// </summary>
public sealed class BackupRestoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("chbackup").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private string NewDir(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Restore_overwrites_matching_files()
    {
        var backup = NewDir("backup");
        var plugins = NewDir("plugins");
        Write(Path.Combine(backup, "Mod.dll"), "original");
        Write(Path.Combine(plugins, "Mod.dll"), "corrupted");

        ModService.RestoreBackup(backup, plugins);

        Assert.Equal("original", File.ReadAllText(Path.Combine(plugins, "Mod.dll")));
    }

    [Fact]
    public void Restore_keeps_files_added_since_the_backup()
    {
        // Restoring is a merge, not a replace. A mod installed after the backup must survive,
        // otherwise "restore" silently becomes "delete everything newer".
        var backup = NewDir("backup");
        var plugins = NewDir("plugins");
        Write(Path.Combine(backup, "Old.dll"), "old");
        Write(Path.Combine(plugins, "New.dll"), "new");

        ModService.RestoreBackup(backup, plugins);

        Assert.True(File.Exists(Path.Combine(plugins, "New.dll")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(plugins, "New.dll")));
        Assert.True(File.Exists(Path.Combine(plugins, "Old.dll")));
    }

    [Fact]
    public void Restore_recreates_nested_folders()
    {
        var backup = NewDir("backup");
        var plugins = NewDir("plugins");
        Write(Path.Combine(backup, "CustomSprites", "st0", "Head", "face.png"), "sprite");

        var restored = ModService.RestoreBackup(backup, plugins);

        Assert.Equal(1, restored);
        Assert.True(File.Exists(Path.Combine(plugins, "CustomSprites", "st0", "Head", "face.png")));
    }

    [Fact]
    public void Restore_counts_every_file_it_copied()
    {
        var backup = NewDir("backup");
        var plugins = NewDir("plugins");
        Write(Path.Combine(backup, "A.dll"), "a");
        Write(Path.Combine(backup, "sub", "B.dll"), "b");
        Write(Path.Combine(backup, "sub", "deep", "C.dll"), "c");

        Assert.Equal(3, ModService.RestoreBackup(backup, plugins));
    }

    [Fact]
    public void Restore_refuses_a_missing_backup()
    {
        var plugins = NewDir("plugins");

        Assert.Throws<DirectoryNotFoundException>(
            () => ModService.RestoreBackup(Path.Combine(_root, "nope"), plugins));
    }

    [Fact]
    public void Restore_refuses_a_missing_plugins_folder()
    {
        var backup = NewDir("backup");

        Assert.Throws<DirectoryNotFoundException>(
            () => ModService.RestoreBackup(backup, Path.Combine(_root, "nope")));
    }

    [Fact]
    public void BackupRoot_stays_under_the_data_directory_by_default()
    {
        // Not beside the executable: a tarball is commonly extracted somewhere read-only, or
        // into a downloads folder the user later clears out.
        var root = ModService.BackupRoot(new Settings { BackupPath = "Backups" });

        Assert.True(Path.IsPathRooted(root));
        Assert.StartsWith(LinuxPaths.AppDataRoot(), root);
        Assert.DoesNotContain(AppContext.BaseDirectory.TrimEnd('/'), root);
    }

    [Fact]
    public void BackupRoot_honours_an_absolute_override()
    {
        var root = ModService.BackupRoot(new Settings { BackupPath = "/mnt/backups/hub" });

        Assert.Equal("/mnt/backups/hub", root);
    }

    [Fact]
    public void Backup_copies_without_touching_the_originals()
    {
        var plugins = NewDir("game", "BepInEx", "plugins");
        Write(Path.Combine(plugins, "Mod.dll"), "content");
        Write(Path.Combine(plugins, "CustomSprites", "st0", "Body", "leg.png"), "sprite");

        var settings = new Settings
        {
            GamePath = Path.Combine(_root, "game"),
            BackupPath = Path.Combine(_root, "backups"),
        };

        var backupPath = new ModService().BackupPlugins(settings);

        Assert.True(File.Exists(Path.Combine(backupPath, "Mod.dll")));
        Assert.True(File.Exists(Path.Combine(backupPath, "CustomSprites", "st0", "Body", "leg.png")));

        // The whole point of this being separate from PurgeToBackup.
        Assert.True(File.Exists(Path.Combine(plugins, "Mod.dll")));
        Assert.Equal("content", File.ReadAllText(Path.Combine(plugins, "Mod.dll")));
    }

    [Fact]
    public void Backup_then_restore_recovers_a_deleted_mod()
    {
        var plugins = NewDir("game", "BepInEx", "plugins");
        Write(Path.Combine(plugins, "Mod.dll"), "content");

        var settings = new Settings
        {
            GamePath = Path.Combine(_root, "game"),
            BackupPath = Path.Combine(_root, "backups"),
        };

        var backupPath = new ModService().BackupPlugins(settings);
        File.Delete(Path.Combine(plugins, "Mod.dll"));

        ModService.RestoreBackup(backupPath, plugins);

        Assert.True(File.Exists(Path.Combine(plugins, "Mod.dll")));
        Assert.Equal("content", File.ReadAllText(Path.Combine(plugins, "Mod.dll")));
    }
}
