using System.IO;
using System.Text.Json;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

public class ProtectedFilesService
{
    private readonly SettingsService _settingsService;
    private readonly ModService _modService;
    private readonly string _manifestPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ProtectedFilesService(SettingsService settingsService, ModService modService)
    {
        _settingsService = settingsService;
        _modService = modService;
        _manifestPath = Path.Combine(settingsService.AppDataPath, "ProtectedFiles.json");
    }

    public List<ProtectedFile> Load() => !File.Exists(_manifestPath)
        ? []
        : JsonSerializer.Deserialize<List<ProtectedFile>>(File.ReadAllText(_manifestPath)) ?? [];

    public void Protect(Settings settings, IEnumerable<string> selectedPaths)
    {
        var pluginsPath = Path.GetFullPath(_modService.GetPluginsPath(settings));
        var protectedRoot = Path.Combine(_settingsService.AppDataPath, settings.ProtectedFilesPath);
        var items = Load();

        var protectedCount = 0;
        foreach (var selectedPath in selectedPaths)
        {
            var sourcePath = Path.GetFullPath(selectedPath);
            if (!sourcePath.StartsWith(pluginsPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose files or folders inside the resolved Plugins folder.");

            var isDirectory = Directory.Exists(sourcePath);
            if (!isDirectory && !File.Exists(sourcePath))
                throw new FileNotFoundException("The selected protected item no longer exists.", sourcePath);

            var relativePath = Path.GetRelativePath(pluginsPath, sourcePath);
            var savedPath = Path.Combine(protectedRoot, relativePath);
            if (isDirectory)
            {
                if (Directory.Exists(savedPath)) Directory.Delete(savedPath, true);
                CopyDirectory(sourcePath, savedPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savedPath)!);
                File.Copy(sourcePath, savedPath, true);
            }

            items.RemoveAll(item => string.Equals(item.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
            items.Add(new ProtectedFile { RelativePath = relativePath, SavedPath = savedPath, IsDirectory = isDirectory });
            protectedCount++;
        }
        File.WriteAllText(_manifestPath, JsonSerializer.Serialize(items, _jsonOptions));
        DebugLogService.Activity("Protected Assets", $"Saved {protectedCount} protected item(s).");
    }

    public int Restore(Settings settings)
    {
        var pluginsPath = _modService.GetPluginsPath(settings);
        var items = Load().Where(item => item.IsDirectory ? Directory.Exists(item.SavedPath) : File.Exists(item.SavedPath)).ToList();

        foreach (var item in items.OrderBy(item => item.RelativePath.Count(character => character == Path.DirectorySeparatorChar)))
        {
            var destinationPath = Path.Combine(pluginsPath, item.RelativePath);
            if (item.IsDirectory)
            {
                if (Directory.Exists(destinationPath)) Directory.Delete(destinationPath, true);
                else if (File.Exists(destinationPath)) File.Delete(destinationPath);
                CopyDirectory(item.SavedPath, destinationPath);
            }
            else
            {
                if (Directory.Exists(destinationPath)) Directory.Delete(destinationPath, true);
                else if (File.Exists(destinationPath)) File.Delete(destinationPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(item.SavedPath, destinationPath);
            }
        }
        DebugLogService.Activity("Protected Assets", $"Restored {items.Count} protected item(s) into Plugins.");
        return items.Count;
    }

    public bool Remove(ProtectedFile item)
    {
        var items = Load();
        var removed = items.RemoveAll(candidate => string.Equals(candidate.RelativePath, item.RelativePath, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;

        var protectedRoot = GetProtectedRoot();
        var savedPath = Path.GetFullPath(Path.Combine(protectedRoot, item.RelativePath));
        if (!savedPath.StartsWith(protectedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The protected item has an invalid saved path.");

        if (Directory.Exists(savedPath)) Directory.Delete(savedPath, true);
        else if (File.Exists(savedPath)) File.Delete(savedPath);
        File.WriteAllText(_manifestPath, JsonSerializer.Serialize(items, _jsonOptions));
        DebugLogService.Activity("Protected Assets", $"Removed saved protected item {item.RelativePath}.");
        return true;
    }

    public string GetProtectedRoot()
    {
        var settings = _settingsService.Load();
        var root = Path.Combine(_settingsService.AppDataPath, settings.ProtectedFilesPath);
        Directory.CreateDirectory(root);
        return Path.GetFullPath(root);
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);
        foreach (var file in Directory.EnumerateFiles(sourcePath)) File.Copy(file, Path.Combine(destinationPath, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(sourcePath)) CopyDirectory(directory, Path.Combine(destinationPath, Path.GetFileName(directory)));
    }
}
