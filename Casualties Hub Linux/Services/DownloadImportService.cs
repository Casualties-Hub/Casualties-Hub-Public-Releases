using System.Collections.Concurrent;
using System.IO;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Watches the downloads folder and offers to install mod archives as they arrive.
/// </summary>
/// <remarks>
/// <para>
/// Two things had to change from the Windows version. It decided a download had finished by
/// opening the file with <c>FileShare.None</c> and seeing whether the open succeeded; Linux uses
/// advisory locking, so that open succeeds while a browser is still writing and the Hub would
/// hand a truncated archive to the installer. Completion is now judged by the file size holding
/// steady instead.
/// </para>
/// <para>
/// The install prompt is also a callback rather than an inline MessageBox, because Avalonia
/// dialogs are async and this runs on a pool thread.
/// </para>
/// </remarks>
public sealed class DownloadImportService : IDisposable
{
    /// <summary>Suffixes browsers use for a download still in flight. Never worth inspecting.</summary>
    private static readonly string[] PartialSuffixes =
        [".part", ".crdownload", ".download", ".tmp", ".partial", ".opdownload"];

    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly UniversalMetadataService _metadataService;

    // Ordinal, not OrdinalIgnoreCase: on a case-sensitive filesystem "Mod.zip" and "mod.zip"
    // are two different downloads and both deserve to be processed.
    private readonly ConcurrentDictionary<string, byte> _inProgress = new(StringComparer.Ordinal);

    private FileSystemWatcher? _watcher;

    public DownloadImportService() => _metadataService = new(_settingsService);

    /// <summary>
    /// Asked whether to install a detected archive, and into which skin slot. Set by the UI, which
    /// owns the dialogs. Left null the service only logs and never installs anything.
    /// </summary>
    public Func<ArchiveInstallPlan, string, Task<(bool Install, string? SkinSlot)>>? DecideAsync { get; set; }

    /// <summary>Raised when an import finishes, so the mods list can refresh.</summary>
    public event Action? ImportCompleted;

    public void Start()
    {
        var settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.DownloadPath) || !Directory.Exists(settings.DownloadPath))
        {
            DebugLogService.Info("Download import watcher was not started: the selected download folder does not exist.");
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(settings.DownloadPath, "*.*")
            {
                // Deliberately no Size filter. On Linux inotify reports size changes on every
                // write, so a large download would fire hundreds of events for no benefit.
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };
            _watcher.Created += (_, e) => QueueImport(e.FullPath);
            // Renames matter more here than on Windows: browsers download to "file.zip.part"
            // and rename on completion, so the rename is often the first supported-name event.
            _watcher.Renamed += (_, e) => QueueImport(e.FullPath);
            _watcher.Error += (_, e) =>
                DebugLogService.Error("Download import watcher stopped; inotify may be out of watches "
                                      + "(raise fs.inotify.max_user_watches)", e.GetException());

            DebugLogService.Info($"Watching download folder for ZIP, 7z, and RAR imports: {settings.DownloadPath}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            DebugLogService.Error("Could not watch the download folder", exception);
        }
    }

    private void QueueImport(string archivePath)
    {
        if (IsPartialDownload(archivePath)) return;
        if (!ModService.IsSupportedArchive(archivePath)) return;
        if (!_inProgress.TryAdd(archivePath, 0)) return;

        DebugLogService.Activity("Download import", $"Detected {Path.GetFileName(archivePath)}; waiting for the download to finish.");
        _ = Task.Run(() => ImportAsync(archivePath));
    }

    private async Task ImportAsync(string archivePath)
    {
        try
        {
            if (!await WaitForDownloadToFinishAsync(archivePath)) return;
            DebugLogService.Activity("Download import", $"Download finished for {Path.GetFileName(archivePath)}.");

            var settings = _settingsService.Load();
            if (!_modService.HasConfiguredPluginsFolder(settings))
            {
                DebugLogService.Info($"Skipped import because the plugins folder is not configured: {Path.GetFileName(archivePath)}");
                return;
            }

            if (DecideAsync is null)
            {
                DebugLogService.Info($"No install prompt is wired up; leaving {Path.GetFileName(archivePath)} alone.");
                return;
            }

            IReadOnlyList<MetadataMod> metadata = UniversalMetadataService.LastSuccessfulMods;
            if (metadata.Count == 0) metadata = await _metadataService.GetModsAsync();

            var plan = _modService.InspectArchive(settings, archivePath, metadata);
            var (install, skinSlot) = await DecideAsync(plan, archivePath);
            if (!install)
            {
                DebugLogService.Info($"User declined the downloaded archive: {Path.GetFileName(archivePath)}");
                return;
            }

            _modService.InstallArchive(settings, archivePath, metadata, skinSlot);

            if (!settings.DisableAutoDeleteImportedParentFiles) ArchiveImportedFile(archivePath);
            else DebugLogService.Info($"Kept the imported archive in the download folder: {Path.GetFileName(archivePath)}");

            DebugLogService.Activity("Download import", $"Completed automatic import for {Path.GetFileName(archivePath)}.");
            ImportCompleted?.Invoke();
        }
        catch (Exception exception)
        {
            DebugLogService.Error($"Automatic import failed for {Path.GetFileName(archivePath)}", exception);
        }
        finally
        {
            _inProgress.TryRemove(archivePath, out _);
        }
    }

    private void ArchiveImportedFile(string archivePath)
    {
        try
        {
            var importedPath = Path.Combine(
                _settingsService.AppDataPath, "ImportedDownloads",
                $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Path.GetFileName(archivePath)}");
            Directory.CreateDirectory(Path.GetDirectoryName(importedPath)!);
            File.Move(archivePath, importedPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Failing to tidy up must not undo a successful install.
            DebugLogService.Error($"Installed {Path.GetFileName(archivePath)} but could not move it out of the download folder", exception);
        }
    }

    public static bool IsPartialDownload(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith('.')
            || PartialSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static Task<bool> WaitForDownloadToFinishAsync(string path) =>
        WaitForStableSizeAsync(path, TimeSpan.FromSeconds(1), requiredStableReads: 3, maxAttempts: 60);

    /// <summary>
    /// Waits until a file's size stops changing, which is how a finished download is recognised here.
    /// </summary>
    /// <remarks>
    /// Windows can simply try an exclusive open, because the writer holds a mandatory lock. Linux
    /// locking is advisory, so that same open succeeds mid-download and the archive would be
    /// installed half-written. Requiring several identical size readings in a row costs a few
    /// seconds and does not depend on the writer cooperating.
    /// Internal so the timing can be shortened in tests.
    /// </remarks>
    internal static async Task<bool> WaitForStableSizeAsync(
        string path, TimeSpan interval, int requiredStableReads, int maxAttempts)
    {
        long lastSize = -1;
        var stableReads = 0;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await Task.Delay(interval);

            long size;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    // Renamed away or cancelled mid-download; nothing left to import.
                    DebugLogService.Info($"Download vanished before it finished: {Path.GetFileName(path)}");
                    return false;
                }
                size = info.Length;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (size > 0 && size == lastSize)
            {
                if (++stableReads >= requiredStableReads) return true;
            }
            else
            {
                stableReads = 0;
            }

            lastSize = size;
        }

        DebugLogService.Info($"Timed out waiting for the download to finish: {Path.GetFileName(path)}");
        return false;
    }

    public void Dispose() => _watcher?.Dispose();
}
