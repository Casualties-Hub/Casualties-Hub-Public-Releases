using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using Casualties_Hub.Models;
using Casualties_Hub.Views;

namespace Casualties_Hub.Services;

public sealed class DownloadImportService : IDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly ModService _modService = new();
    private readonly UniversalMetadataService _metadataService;
    private readonly ConcurrentDictionary<string, byte> _inProgress = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;

    public DownloadImportService() => _metadataService = new(_settingsService);

    public void Start()
    {
        var settings = _settingsService.Load();
        if (string.IsNullOrWhiteSpace(settings.DownloadPath) || !Directory.Exists(settings.DownloadPath))
        {
            DebugLogService.Info("Download import watcher was not started: the selected download folder does not exist.");
            return;
        }

        _watcher = new FileSystemWatcher(settings.DownloadPath, "*.*")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };
        _watcher.Created += OnArchiveDetected;
        _watcher.Renamed += (_, e) => QueueImport(e.FullPath);
        _watcher.Error += (_, e) => DebugLogService.Error("Download import watcher encountered an error", e.GetException());
        DebugLogService.Info($"Watching download folder for ZIP, 7z, and RAR imports: {settings.DownloadPath}");
    }

    private void OnArchiveDetected(object sender, FileSystemEventArgs e) => QueueImport(e.FullPath);

    private void QueueImport(string archivePath)
    {
        if (!ModService.IsSupportedArchive(archivePath) || !_inProgress.TryAdd(archivePath, 0)) return;
        DebugLogService.Activity("Download import", $"Detected archive {Path.GetFileName(archivePath)}; waiting for the download to finish.");
        _ = Task.Run(async () =>
        {
            try
            {
                if (!await WaitForDownloadToFinish(archivePath)) return;
                DebugLogService.Activity("Download import", $"Download finished for {Path.GetFileName(archivePath)}.");
                var settings = _settingsService.Load();
                if (!_modService.HasConfiguredGameFolder(settings))
                {
                    DebugLogService.Info($"Skipped archive import because Plugins is not configured: {Path.GetFileName(archivePath)}");
                    return;
                }

                IReadOnlyList<MetadataMod> metadata = UniversalMetadataService.LastSuccessfulMods;
                if (metadata.Count == 0) metadata = await _metadataService.GetModsAsync();
                var plan = _modService.InspectArchive(settings, archivePath, metadata);
                DebugLogService.Activity("Download import", $"Showing the install prompt for {Path.GetFileName(archivePath)}.");
                var decision = Application.Current.Dispatcher.Invoke(() =>
                {
                    BringHubToFront();
                    return GetInstallDecision(plan, archivePath);
                });
                if (!decision.ShouldInstall)
                {
                    DebugLogService.Info($"User declined downloaded archive install: {Path.GetFileName(archivePath)}");
                    return;
                }

                _modService.InstallArchive(settings, archivePath, metadata, decision.SkinSlot);
                if (!settings.DisableAutoDeleteImportedParentFiles)
                {
                    var importedPath = Path.Combine(_settingsService.AppDataPath, "ImportedDownloads", $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Path.GetFileName(archivePath)}");
                    Directory.CreateDirectory(Path.GetDirectoryName(importedPath)!);
                    File.Move(archivePath, importedPath, true);
                }
                else
                {
                    DebugLogService.Info($"Kept imported archive in the download inbox (DADIPF enabled): {Path.GetFileName(archivePath)}");
                }
                DebugLogService.Info($"Imported downloaded archive: {Path.GetFileName(archivePath)}");
                DebugLogService.Activity("Download import", $"Completed automatic import for {Path.GetFileName(archivePath)}.");
            }
            catch (Exception ex)
            {
                DebugLogService.Error($"Automatic archive import failed for {Path.GetFileName(archivePath)}", ex);
            }
            finally { _inProgress.TryRemove(archivePath, out _); }
        });
    }

    private static (bool ShouldInstall, string? SkinSlot) GetInstallDecision(ArchiveInstallPlan plan, string archivePath)
    {
        if (plan.Kind == ArchiveInstallKind.Unsupported)
        {
            MessageBox.Show($"'{Path.GetFileName(archivePath)}' was downloaded, but {plan.Description}", "Unsupported archive layout", MessageBoxButton.OK, MessageBoxImage.Information);
            return (false, null);
        }

        var replacementText = plan.ExistingFilesToReplace.Count == 0
            ? ""
            : $"\n\n{plan.ExistingFilesToReplace.Count} existing file(s) matching the new archive will be replaced. BepInEx itself will not be deleted.";
        if (MessageBox.Show($"{plan.Description}{replacementText}{plan.DependencyPrompt}\n\nInstall '{Path.GetFileName(archivePath)}'?", "Casualties Hub download detected", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return (false, null);

        if (!plan.RequiresSkinSlot) return (true, null);
        var slotDialog = new SkinSlotDialog { Owner = Application.Current.MainWindow };
        if (slotDialog.ShowDialog() != true) return (false, null);
        if (slotDialog.SelectedSlotIsOccupied
            && MessageBox.Show($"The current CustomSprites\\{slotDialog.SelectedSlot} contents will be permanently replaced. Continue?", "Replace sprite slot", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return (false, null);
        return (true, slotDialog.SelectedSlot);
    }

    private static void BringHubToFront()
    {
        if (Application.Current.MainWindow is not { } window) return;
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    private static async Task<bool> WaitForDownloadToFinish(string path)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return stream.Length > 0;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        DebugLogService.Info($"Timed out waiting for download to complete: {Path.GetFileName(path)}");
        return false;
    }

    public void Dispose() => _watcher?.Dispose();
}
