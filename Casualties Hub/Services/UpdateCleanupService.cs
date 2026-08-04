using System.IO;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// UpdateInstaller downloads and extracts every update into its own GUID folder under Temp,
/// and its helper script only deletes itself, never the staged ZIP and extracted files beside
/// it. This finds those leftovers so the player can reclaim the space.
/// </summary>
public static class UpdateCleanupService
{
    public static IReadOnlyList<UpdateStagingFolder> ScanStagingFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "CasualtiesHub");
        var found = new List<UpdateStagingFolder>();
        found.AddRange(ScanKind(Path.Combine(root, "Updates"), "Downloaded update"));
        found.AddRange(ScanKind(Path.Combine(root, "LocalUpdates"), "Local release install"));
        return found;
    }

    public static void Delete(IEnumerable<string> folders)
    {
        foreach (var folder in folders)
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                DebugLogService.Activity("Update cleanup", $"Removed leftover update files: {folder}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                DebugLogService.Error($"Could not remove leftover update folder {folder}", exception);
            }
        }
    }

    private static IEnumerable<UpdateStagingFolder> ScanKind(string kindRoot, string kindLabel)
    {
        if (!Directory.Exists(kindRoot)) yield break;

        IEnumerable<string> folders;
        try { folders = Directory.EnumerateDirectories(kindRoot).ToList(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { yield break; }

        foreach (var folder in folders)
        {
            long size;
            DateTimeOffset lastWrite;
            try
            {
                size = EnumerateFilesSafely(folder).Sum(file => new FileInfo(file).Length);
                lastWrite = Directory.GetLastWriteTimeUtc(folder);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { continue; }

            if (size == 0) continue;
            yield return new UpdateStagingFolder
            {
                Path = folder,
                Kind = kindLabel,
                SizeBytes = size,
                LastWriteTimeUtc = lastWrite
            };
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string folder)
    {
        try { return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToList(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return []; }
    }
}
