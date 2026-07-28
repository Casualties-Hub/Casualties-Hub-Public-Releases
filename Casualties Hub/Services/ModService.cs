using System.IO;
using System.IO.Compression;
using System.Reflection;
using Casualties_Hub.Models;
using Mono.Cecil;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Casualties_Hub.Services;

public class ModService
{
    public static event EventHandler? PluginFilesChanged;

    public static void NotifyPluginFilesChanged() => PluginFilesChanged?.Invoke(null, EventArgs.Empty);
    // The player can select the game root, BepInEx, or BepInEx\Plugins.
    public string GetPluginsPath(Settings settings)
    {
        var selectedPath = settings.GamePath;
        if (string.IsNullOrWhiteSpace(selectedPath)) return "";

        var normalized = Path.GetFullPath(selectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (Path.GetFileName(normalized).Equals("Plugins", StringComparison.OrdinalIgnoreCase)) return normalized;
        if (Path.GetFileName(normalized).Equals("BepInEx", StringComparison.OrdinalIgnoreCase)) return Path.Combine(normalized, "Plugins");
        return Path.Combine(normalized, "BepInEx", "Plugins");
    }
    public bool HasConfiguredPluginsFolder(Settings settings) => !string.IsNullOrWhiteSpace(settings.GamePath) && Directory.Exists(GetPluginsPath(settings));
    public bool HasConfiguredGameFolder(Settings settings) => !string.IsNullOrWhiteSpace(settings.GamePath) && Directory.Exists(settings.GamePath);

    public string GetGameRoot(Settings settings)
    {
        var selectedPath = Path.GetFullPath(settings.GamePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (Path.GetFileName(selectedPath).Equals("Plugins", StringComparison.OrdinalIgnoreCase)) return Directory.GetParent(selectedPath)!.Parent!.FullName;
        if (Path.GetFileName(selectedPath).Equals("BepInEx", StringComparison.OrdinalIgnoreCase)) return Directory.GetParent(selectedPath)!.FullName;
        return selectedPath;
    }

    public string? GetInstallationWarning(string zipPath)
    {
        var name = Path.GetFileNameWithoutExtension(zipPath);
        if (name.Contains("Krokosha", StringComparison.OrdinalIgnoreCase) || name.Contains("CasualtiesMP", StringComparison.OrdinalIgnoreCase) || name.Contains("Multiplayer", StringComparison.OrdinalIgnoreCase))
            return "This appears to be the Multiplayer mod. It has specific install instructions and will be extracted into the Casualties Unknown game folder, not directly into Plugins.";
        return null;
    }

    public static bool IsSupportedArchive(string path) =>
        new[] { ".zip", ".7z", ".rar" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public ArchiveInstallPlan InspectArchive(Settings settings, string archivePath, IReadOnlyList<MetadataMod> metadata)
    {
        DebugLogService.Activity("Archive", $"Inspecting {Path.GetFileName(archivePath)}.");
        EnsureSupportedArchive(archivePath);
        var stagingPath = ExtractToStaging(archivePath);
        try
        {
            var plan = CreateInstallPlan(settings, stagingPath, metadata);
            DebugLogService.Activity("Archive", $"Install plan: {plan.Kind}; {plan.ExistingFilesToReplace.Count} existing file(s) will be replaced.");
            return plan;
        }
        finally { DeleteDirectoryIfExists(stagingPath); }
    }

    public void InstallArchive(Settings settings, string archivePath, IReadOnlyList<MetadataMod> metadata, string? skinSlot = null)
    {
        DebugLogService.Activity("Archive", $"Installing {Path.GetFileName(archivePath)}.");
        EnsureSupportedArchive(archivePath);
        var stagingPath = ExtractToStaging(archivePath);
        try
        {
            var plan = CreateInstallPlan(settings, stagingPath, metadata);
            if (plan.Kind == ArchiveInstallKind.Unsupported)
                throw new InvalidDataException(plan.Description);

            foreach (var existingFile in plan.ExistingFilesToReplace)
                File.Delete(existingFile);

            var pluginsPath = GetPluginsPath(settings);
            Directory.CreateDirectory(pluginsPath);

            switch (plan.Kind)
            {
                case ArchiveInstallKind.PluginDll:
                    CopyDirectoryContentsExcludingText(stagingPath, pluginsPath);
                    break;
                case ArchiveInstallKind.BepInExLayout:
                    InstallBepInExLayout(settings, stagingPath);
                    break;
                case ArchiveInstallKind.CustomSprite:
                    InstallCustomSprite(pluginsPath, stagingPath, skinSlot);
                    break;
            }
            NotifyPluginFilesChanged();
            DebugLogService.Activity("Archive", $"Finished installing {Path.GetFileName(archivePath)} as {plan.Kind}.");
        }
        finally { DeleteDirectoryIfExists(stagingPath); }
    }

    public List<string> GetInstalledMods(Settings settings)
    {
        if (!HasConfiguredPluginsFolder(settings)) return [];

        return Directory.EnumerateFileSystemEntries(GetPluginsPath(settings))
            .Where(entry => Directory.Exists(entry) || IsPluginDllFile(entry))
            .Select(entry => Path.GetFileName(entry)!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<InstalledMod> GetInstalledModsWithMetadata(Settings settings, IReadOnlyList<MetadataMod> metadata)
    {
        if (!HasConfiguredPluginsFolder(settings)) return [];

        var entries = Directory.EnumerateFileSystemEntries(GetPluginsPath(settings))
            .Where(entry => Directory.Exists(entry) || IsPluginDllFile(entry));

        var mods = entries.Select(entry => CreateInstalledMod(entry, metadata)).ToList();
        // A disabled DLL cannot satisfy a dependency or participate in an active conflict check.
        var activeNames = mods.Where(mod => !mod.IsDisabled).Select(mod => mod.Name).ToList();
        var ignoredDependencyNames = settings.IgnoredDependencyNames ?? [];
        foreach (var mod in mods)
        {
            if (mod.IsDisabled)
            {
                mod.MissingDependencies = [];
                mod.IncompatibleWith = [];
                mod.KnownBugs = [];
                continue;
            }
            mod.MissingDependencies = mod.RequiredDependencies
                .Where(dependency => !activeNames.Any(name => DependencyCatalog.NamesMatch(name, dependency.Name)))
                .Where(dependency => !ignoredDependencyNames.Any(name => DependencyCatalog.NamesMatch(name, dependency.Name)))
                .ToList();
            mod.IncompatibleWith = IncompatibilityCatalog.GetConflicts(mod.Name, activeNames);
            mod.KnownBugs = CompatibilityFeedService.GetKnownBugs(mod.Name);
        }
        return mods.OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public int DeleteAllPluginContents(Settings settings)
    {
        var pluginsPath = GetPluginsPath(settings);
        if (!Directory.Exists(pluginsPath))
            throw new DirectoryNotFoundException("The configured BepInEx\\Plugins folder was not found.");

        var entries = Directory.EnumerateFileSystemEntries(pluginsPath).ToList();
        foreach (var entry in entries)
        {
            if (Directory.Exists(entry)) Directory.Delete(entry, true);
            else File.Delete(entry);
        }
        NotifyPluginFilesChanged();
        DebugLogService.Activity("Local Mods", $"Deleted all plugin contents ({entries.Count} item(s)).");
        return entries.Count;
    }

    public void ToggleModDisabled(InstalledMod mod)
    {
        if (mod.PluginDllPaths.Count == 0)
            throw new InvalidOperationException("This mod has no managed DLL file to enable or disable.");

        foreach (var path in mod.PluginDllPaths)
        {
            if (!File.Exists(path)) continue;
            var destination = mod.IsDisabled
                ? path[..^".disabled".Length]
                : path + ".disabled";
            if (File.Exists(destination))
                throw new IOException($"Cannot rename '{Path.GetFileName(path)}' because '{Path.GetFileName(destination)}' already exists.");
            File.Move(path, destination);
        }
        NotifyPluginFilesChanged();
        DebugLogService.Activity("Local Mods", $"{(mod.IsDisabled ? "Enabled" : "Disabled")} {mod.Name}.");
    }

    public int SetAllModsDisabled(Settings settings, bool disabled)
    {
        var pluginsPath = GetPluginsPath(settings);
        if (!Directory.Exists(pluginsPath)) throw new DirectoryNotFoundException("The configured BepInEx\\Plugins folder was not found.");

        var files = disabled
            ? Directory.EnumerateFiles(pluginsPath, "*.dll", SearchOption.AllDirectories).ToList()
            : Directory.EnumerateFiles(pluginsPath, "*.dll.disabled", SearchOption.AllDirectories).ToList();
        foreach (var path in files)
        {
            var destination = disabled ? path + ".disabled" : path[..^".disabled".Length];
            if (File.Exists(destination)) throw new IOException($"Cannot rename '{Path.GetFileName(path)}' because '{Path.GetFileName(destination)}' already exists.");
            File.Move(path, destination);
        }
        NotifyPluginFilesChanged();
        DebugLogService.Activity("Local Mods", $"{(disabled ? "Disabled" : "Enabled")} all local plugin DLLs ({files.Count} file(s)).");
        return files.Count;
    }

    public void DeleteInstalledMod(InstalledMod mod)
    {
        if (string.IsNullOrWhiteSpace(mod.SourceEntryPath))
            throw new InvalidOperationException("This mod does not have a known local file or folder.");
        if (Directory.Exists(mod.SourceEntryPath)) Directory.Delete(mod.SourceEntryPath, true);
        else if (File.Exists(mod.SourceEntryPath)) File.Delete(mod.SourceEntryPath);
        else throw new FileNotFoundException("The mod's local file or folder could not be found.", mod.SourceEntryPath);
        NotifyPluginFilesChanged();
        DebugLogService.Activity("Local Mods", $"Deleted {mod.Name}.");
    }

    public void InstallZip(Settings settings, string zipPath)
    {
        DebugLogService.Activity("Archive", $"Installing legacy ZIP {Path.GetFileName(zipPath)}.");
        var pluginsPath = GetPluginsPath(settings);
        if (string.IsNullOrWhiteSpace(pluginsPath)) throw new DirectoryNotFoundException("Choose a game, BepInEx, or Plugins folder first.");
        Directory.CreateDirectory(pluginsPath);
        var targetPath = GetInstallationWarning(zipPath) is null ? pluginsPath : GetGameRoot(settings);

        var stagingPath = Path.Combine(Path.GetTempPath(), "CasualtiesHub", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingPath);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, stagingPath);
            var entries = Directory.EnumerateFileSystemEntries(stagingPath).ToList();
            if (entries.Count == 0) throw new InvalidDataException("The ZIP file is empty.");

            foreach (var entry in entries)
            {
                var destination = Path.Combine(targetPath, Path.GetFileName(entry));
                if (Directory.Exists(entry)) CopyDirectory(entry, destination);
                else File.Copy(entry, destination, true);
            }
            NotifyPluginFilesChanged();
            DebugLogService.Activity("Archive", $"Finished installing legacy ZIP {Path.GetFileName(zipPath)}.");
        }
        finally { if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, true); }
    }

    private ArchiveInstallPlan CreateInstallPlan(Settings settings, string stagingPath, IReadOnlyList<MetadataMod> metadata)
    {
        var dllPaths = Directory.EnumerateFiles(stagingPath, "*.dll", SearchOption.AllDirectories).ToList();
        var dllNames = dllPaths.Select(Path.GetFileName).Where(name => name is not null).Cast<string>().ToList();
        var bepinexDirectory = FindNamedDirectory(stagingPath, "BepInEx");
        var pluginsDirectory = FindNamedDirectory(stagingPath, "Plugins");
        var customSprite = Directory.EnumerateFiles(stagingPath, "experimentCrus.png", SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetFileName(path).Equals("experimentCrus.png", StringComparison.OrdinalIgnoreCase));

        var kind = bepinexDirectory is not null || pluginsDirectory is not null
            ? ArchiveInstallKind.BepInExLayout
            : dllNames.Count > 0
                ? ArchiveInstallKind.PluginDll
                : customSprite is not null
                    ? ArchiveInstallKind.CustomSprite
                    : ArchiveInstallKind.Unsupported;
        var matchingMetadata = metadata.Where(mod => mod.DllNames.Any(modDll => dllNames.Any(archiveDll => modDll.Equals(archiveDll, StringComparison.OrdinalIgnoreCase)))).ToList();
        var replacementFiles = FindExistingIncomingFiles(settings, stagingPath, kind);
        var matchingNames = matchingMetadata.Select(mod => mod.Name).ToList();

        return new ArchiveInstallPlan
        {
            Kind = kind,
            DllNames = dllNames,
            MatchingModNames = matchingNames,
            ExistingFilesToReplace = replacementFiles,
            KnownDependencies = DependencyCatalog.GetRequirements(matchingNames),
            NeedsManualDependencyReview = !DependencyCatalog.HasEntryForAny(matchingNames)
        };
    }

    private void InstallBepInExLayout(Settings settings, string stagingPath)
    {
        var bepinexDirectory = FindNamedDirectory(stagingPath, "BepInEx");
        if (bepinexDirectory is not null)
        {
            var targetBepinex = Path.Combine(GetGameRoot(settings), "BepInEx");
            Directory.CreateDirectory(targetBepinex);
            CopyDirectoryContentsExcludingText(bepinexDirectory, targetBepinex);
            return;
        }

        var pluginsDirectory = FindNamedDirectory(stagingPath, "Plugins");
        if (pluginsDirectory is null) throw new InvalidDataException("The archive layout could not be located.");
        CopyDirectoryContentsExcludingText(pluginsDirectory, GetPluginsPath(settings));
    }

    private static void InstallCustomSprite(string pluginsPath, string stagingPath, string? skinSlot)
    {
        if (string.IsNullOrWhiteSpace(skinSlot) || !System.Text.RegularExpressions.Regex.IsMatch(skinSlot, "^st[0-9]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new InvalidDataException("Choose a CustomSprites skin slot from st0 through st9.");

        var sourceFile = Directory.EnumerateFiles(stagingPath, "experimentCrus.png", SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetFileName(path).Equals("experimentCrus.png", StringComparison.OrdinalIgnoreCase));
        if (sourceFile is null) throw new InvalidDataException("experimentCrus.png was not found in the archive.");

        // Most skin archives put experimentCrus.png inside Head/ or Body/.  Copying
        // from that immediate directory used to flatten the layout into st# and broke
        // those skins.  Use the nearest parent that contains Head/ or Body/ instead.
        // Archives that intentionally use loose images still copy those loose images as-is.
        var sourceRoot = FindCustomSpriteRoot(stagingPath, Path.GetDirectoryName(sourceFile)!);
        var destination = Path.Combine(pluginsPath, "CustomSprites", skinSlot.ToLowerInvariant());
        DeleteDirectoryIfExists(destination);
        Directory.CreateDirectory(destination);
        CopyDirectoryContentsExcludingText(sourceRoot, destination);
        DebugLogService.Activity("CustomSprites", $"Installed skin layout from {Path.GetRelativePath(stagingPath, sourceRoot)} into {skinSlot} without flattening folders.");
    }

    private static string FindCustomSpriteRoot(string stagingPath, string experimentDirectory)
    {
        var current = new DirectoryInfo(experimentDirectory);
        var stagingFullPath = Path.GetFullPath(stagingPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (current is not null && current.FullName.StartsWith(stagingFullPath, StringComparison.OrdinalIgnoreCase))
        {
            var hasSpriteCategoryFolder = current.EnumerateDirectories().Any(directory =>
                directory.Name.Equals("Head", StringComparison.OrdinalIgnoreCase)
                || directory.Name.Equals("Body", StringComparison.OrdinalIgnoreCase));
            if (hasSpriteCategoryFolder) return current.FullName;
            if (string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), stagingFullPath, StringComparison.OrdinalIgnoreCase))
                break;
            current = current.Parent;
        }
        return experimentDirectory;
    }

    private List<string> FindExistingIncomingFiles(Settings settings, string stagingPath, ArchiveInstallKind kind)
    {
        if (!HasConfiguredGameFolder(settings)) return [];

        IEnumerable<(string SourceRoot, string DestinationRoot)> mappings = kind switch
        {
            ArchiveInstallKind.PluginDll => [(stagingPath, GetPluginsPath(settings))],
            ArchiveInstallKind.BepInExLayout when FindNamedDirectory(stagingPath, "BepInEx") is { } bepinex =>
                [(bepinex, Path.Combine(GetGameRoot(settings), "BepInEx"))],
            ArchiveInstallKind.BepInExLayout when FindNamedDirectory(stagingPath, "Plugins") is { } plugins =>
                [(plugins, GetPluginsPath(settings))],
            _ => []
        };

        var incomingFiles = new List<string>();
        foreach (var (sourceRoot, destinationRoot) in mappings)
        {
            var files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Where(path => !Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase));
            foreach (var sourceFile in files)
            {
                var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
                var destinationFile = Path.Combine(destinationRoot, relativePath);
                if (File.Exists(destinationFile)) incomingFiles.Add(destinationFile);

                // A disabled plugin is stored as Foo.dll.disabled. Installing a newer
                // Foo.dll must retire that old disabled copy so both versions cannot remain.
                if (destinationFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var disabledDestination = destinationFile + ".disabled";
                    if (File.Exists(disabledDestination)) incomingFiles.Add(disabledDestination);
                }
            }
        }
        return incomingFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? FindNamedDirectory(string root, string directoryName) =>
        Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetFileName(path).Equals(directoryName, StringComparison.OrdinalIgnoreCase));

    private static string ExtractToStaging(string archivePath)
    {
        var stagingPath = Path.Combine(Path.GetTempPath(), "CasualtiesHub", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingPath);
        try
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath);
            archive.WriteToDirectory(stagingPath, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true,
                CheckCrc = true
            });
            return stagingPath;
        }
        catch
        {
            DeleteDirectoryIfExists(stagingPath);
            throw;
        }
    }

    private static void CopyDirectoryContents(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static void CopyDirectoryContentsExcludingText(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (Path.GetExtension(file).Equals(".txt", StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectoryContentsExcludingText(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static void EnsureSupportedArchive(string archivePath)
    {
        if (!File.Exists(archivePath)) throw new FileNotFoundException("The selected archive was not found.", archivePath);
        if (!IsSupportedArchive(archivePath)) throw new InvalidDataException("Supported archives are .zip, .7z, and .rar.");
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    public string PurgeToBackup(Settings settings)
    {
        var pluginsPath = GetPluginsPath(settings);
        if (!Directory.Exists(pluginsPath)) throw new DirectoryNotFoundException("The configured Plugins folder was not found.");
        var backupPath = Path.Combine(AppContext.BaseDirectory, settings.BackupPath, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        Directory.CreateDirectory(backupPath);

        foreach (var entry in Directory.EnumerateFileSystemEntries(pluginsPath)
                     .Where(entry => Directory.Exists(entry) || Path.GetExtension(entry).Equals(".dll", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var destination = Path.Combine(backupPath, Path.GetFileName(entry));
            if (Directory.Exists(entry))
            {
                CopyDirectory(entry, destination);
                Directory.Delete(entry, true);
            }
            else
            {
                File.Copy(entry, destination, true);
                File.Delete(entry);
            }
        }
        DebugLogService.Activity("Local Mods", "Purged managed plugin entries to a backup folder.");
        return backupPath;
    }

    private static InstalledMod CreateInstalledMod(string entry, IReadOnlyList<MetadataMod> metadata)
    {
        var dllPaths = Directory.Exists(entry)
            ? Directory.EnumerateFiles(entry, "*", SearchOption.AllDirectories).Where(IsPluginDllFile).ToList()
            : [entry];

        foreach (var dllPath in dllPaths)
        {
            var dllName = GetMetadataDllName(dllPath);
            var metadataMod = FindBestMetadataMatch(metadata, dllName);
            if (metadataMod is null) continue;

            var versionInfo = ReadDllVersionInfo(dllPath);
            var installedSignals = BuildInstalledSignals(versionInfo);
            var expectedSignals = BuildExpectedSignals(metadataMod, dllName);
            var installedVersion = HighestVersion(installedSignals.Select(signal => signal.Version).ToArray());
            var expectedVersion = HighestVersion(expectedSignals.Select(signal => signal.Version).ToArray());
            var fileModifiedUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(dllPath), TimeSpan.Zero);
            var isDisabled = IsDisabledDll(dllPath);
            var hasPlaceholderVersion = installedSignals.Count > 0 && installedSignals.All(signal => IsZeroVersion(signal.Version));
            var matchingSignals = hasPlaceholderVersion ? [] : FindMatchingSignals(installedSignals, expectedSignals);
            var isNewerThanMetadata = !hasPlaceholderVersion && IsAnySignalNewer(installedSignals, expectedSignals);
            var isUpToDateByDate = metadataMod.LatestFileModifiedUtc is { } expectedModifiedUtc && fileModifiedUtc >= expectedModifiedUtc;
            var isUpToDate = !isDisabled && !hasPlaceholderVersion && (matchingSignals.Count > 0 || isNewerThanMetadata || isUpToDateByDate);
            var isOutOfDate = !isDisabled && !hasPlaceholderVersion && !isUpToDate && IsEveryInstalledSignalOlder(installedSignals, expectedSignals);
            return new InstalledMod
            {
                Name = metadataMod.Name,
                InstalledVersion = installedVersion,
                ExpectedVersion = expectedVersion,
                AssemblyVersion = versionInfo.AssemblyVersion,
                BepInExVersion = versionInfo.BepInExVersion,
                MetadataId = metadataMod.Id,
                ModGuid = versionInfo.ModGuid,
                FileModifiedUtc = fileModifiedUtc,
                ExpectedModifiedUtc = metadataMod.LatestFileModifiedUtc,
                IsUpToDate = isUpToDate,
                IsOutOfDate = isOutOfDate,
                IsDisabled = isDisabled,
                UpdateStatusLabel = isDisabled
                    ? "Disabled — re-enable this mod before checking for or installing updates."
                    : hasPlaceholderVersion
                    ? "Version 0.0.0.0 is a placeholder; update status is not checked."
                    : GetUpdateStatusLabel(matchingSignals, isNewerThanMetadata, isUpToDateByDate, installedSignals, expectedSignals),
                NexusUrl = metadataMod.NexusDownloadPageUrl,
                PluginDllPaths = dllPaths,
                SourceEntryPath = entry,
                RequiredDependencies = DependencyCatalog.GetRequirements([metadataMod.Name]),
                Description = metadataMod.Description
            };
        }

        return new InstalledMod { Name = Path.GetFileName(entry) ?? "Unknown mod" };
    }

    // Metadata can list bundled dependency DLLs on more than one Nexus entry. For example,
    // Monster Energy ships CUCoreLib.dll in its archive, but CUCoreLib is also its own mod.
    // Prefer the mod whose name is the DLL's own name, then the least ambiguous DLL listing.
    private static MetadataMod? FindBestMetadataMatch(IReadOnlyList<MetadataMod> metadata, string dllName)
    {
        var candidates = metadata
            .Where(mod => mod.DllNames.Any(name => name.Equals(dllName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (candidates.Count == 0) return null;

        var dllStem = Path.GetFileNameWithoutExtension(dllName);
        return candidates.FirstOrDefault(mod => NamesEquivalent(mod.Name, dllStem))
            ?? candidates.FirstOrDefault(mod => mod.DllNames.Count == 1)
            ?? candidates.OrderBy(mod => mod.DllNames.Count).First();
    }

    private static bool NamesEquivalent(string first, string second)
    {
        static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return Normalize(first).Equals(Normalize(second), StringComparison.Ordinal);
    }

    private static bool IsPluginDllFile(string path) =>
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase);

    private static bool IsDisabledDll(string path) => path.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase);

    private static string GetMetadataDllName(string path) => IsDisabledDll(path)
        ? Path.GetFileName(path)[..^".disabled".Length]
        : Path.GetFileName(path);

    // Mono.Cecil reads the IL metadata directly; it never loads a third-party
    // plugin into the Hub process just to inspect its BepInEx attribute.
    private static DllVersionInfo ReadDllVersionInfo(string dllPath)
    {
        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadingMode = ReadingMode.Deferred, ReadSymbols = false });
            var bepinPluginAttribute = EnumerateTypes(assembly.MainModule.Types)
                .SelectMany(type => type.CustomAttributes)
                .FirstOrDefault(attribute => attribute.AttributeType.FullName.EndsWith(".BepInPlugin", StringComparison.Ordinal));
            var bepinExVersion = bepinPluginAttribute is not null && bepinPluginAttribute.ConstructorArguments.Count >= 3
                ? bepinPluginAttribute.ConstructorArguments[2].Value?.ToString()
                : null;
            var modGuid = bepinPluginAttribute is not null && bepinPluginAttribute.ConstructorArguments.Count >= 1
                ? bepinPluginAttribute.ConstructorArguments[0].Value?.ToString()
                : null;
            return new DllVersionInfo(assembly.Name.Version?.ToString(), bepinExVersion, modGuid);
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new DllVersionInfo(null, null, null);
        }
    }

    private static IEnumerable<TypeDefinition> EnumerateTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in EnumerateTypes(type.NestedTypes))
                yield return nested;
        }
    }

    private static List<VersionSignal> BuildInstalledSignals(DllVersionInfo versionInfo) =>
        new List<VersionSignal>
        {
            new("Assembly version", versionInfo.AssemblyVersion),
            new("BepInEx plugin version", versionInfo.BepInExVersion)
        }.Where(signal => TryParseVersion(signal.Version, out _)).ToList();

    private static List<VersionSignal> BuildExpectedSignals(MetadataMod mod, string dllName)
    {
        var matchingDllVersion = mod.DllVersions.FirstOrDefault(pair => pair.Key.Equals(dllName, StringComparison.OrdinalIgnoreCase)).Value;
        return new List<VersionSignal>
        {
            new("metadata DLL version", matchingDllVersion),
            new("metadata mod DLL version", mod.DllVersion),
            new("metadata BepInEx version", mod.BepinexVersion),
            new("metadata page version", mod.Version)
        }.Where(signal => TryParseVersion(signal.Version, out _)).ToList();
    }

    private static List<string> FindMatchingSignals(IEnumerable<VersionSignal> installedSignals, IEnumerable<VersionSignal> expectedSignals)
    {
        var matches = new List<string>();
        foreach (var installed in installedSignals)
        foreach (var expected in expectedSignals)
        {
            if (!TryParseVersion(installed.Version, out var installedVersion) || !TryParseVersion(expected.Version, out var expectedVersion)) continue;
            if (installedVersion == expectedVersion)
                matches.Add($"Up to date: {installed.Source} {installed.Version} matches {expected.Source} {expected.Version}");
        }
        return matches;
    }

    private static bool IsAnySignalNewer(IEnumerable<VersionSignal> installedSignals, IEnumerable<VersionSignal> expectedSignals) =>
        installedSignals.Any(installed => expectedSignals.Any(expected =>
            TryParseVersion(installed.Version, out var installedVersion)
            && TryParseVersion(expected.Version, out var expectedVersion)
            && installedVersion > expectedVersion));

    private static bool IsEveryInstalledSignalOlder(IReadOnlyCollection<VersionSignal> installedSignals, IReadOnlyCollection<VersionSignal> expectedSignals) =>
        installedSignals.Count > 0 && expectedSignals.Count > 0 && installedSignals.All(installed => expectedSignals.All(expected =>
            TryParseVersion(installed.Version, out var installedVersion)
            && TryParseVersion(expected.Version, out var expectedVersion)
            && installedVersion < expectedVersion));

    private static string GetUpdateStatusLabel(IReadOnlyList<string> matches, bool isNewerThanMetadata, bool isUpToDateByDate, IReadOnlyList<VersionSignal> installedSignals, IReadOnlyList<VersionSignal> expectedSignals)
    {
        if (matches.Count > 0) return matches[0];
        if (isNewerThanMetadata) return "Up to date: installed version is newer than the live metadata.";
        if (isUpToDateByDate) return "Up to date: installed file modified date matches the live metadata date.";
        if (installedSignals.Count == 0 || expectedSignals.Count == 0) return "No comparable update data was found.";
        var newestInstalled = HighestSignal(installedSignals)!;
        var newestExpected = HighestSignal(expectedSignals)!;
        return $"Out of date: {newestInstalled.Source} {newestInstalled.Version} is below {newestExpected.Source} {newestExpected.Version}.";
    }

    private static VersionSignal? HighestSignal(IEnumerable<VersionSignal> signals) =>
        signals.Aggregate<VersionSignal, VersionSignal?>(null, (highest, current) =>
        {
            if (highest is null) return current;
            return TryParseVersion(current.Version, out var currentVersion)
                   && TryParseVersion(highest.Version, out var highestVersion)
                   && currentVersion > highestVersion
                ? current
                : highest;
        });

    private sealed record VersionSignal(string Source, string? Version);
    private sealed record DllVersionInfo(string? AssemblyVersion, string? BepInExVersion, string? ModGuid);

    private static string? HighestVersion(params string?[] candidates)
    {
        string? highestValue = null;
        Version? highestVersion = null;
        foreach (var candidate in candidates)
        {
            if (!TryParseVersion(candidate, out var parsed)) continue;
            if (highestVersion is null || parsed > highestVersion)
            {
                highestVersion = parsed;
                highestValue = candidate;
            }
        }
        return highestValue;
    }

    private static bool TryParseVersion(string? input, out Version version)
    {
        version = new Version();
        if (string.IsNullOrWhiteSpace(input)) return false;
        var clean = input.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(clean, out var parsed) || parsed is null) return false;
        version = parsed;
        return true;
    }

    private static bool IsZeroVersion(string? input) =>
        TryParseVersion(input, out var version) && version.Major == 0 && version.Minor == 0 && version.Build <= 0 && version.Revision <= 0;

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
