using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using Casualties_Hub_Installer.Models;

namespace Casualties_Hub_Installer.Services;

/// <summary>Downloads a published Hub ZIP, verifies it when a checksum is available, and extracts it into a selected folder.</summary>
public sealed class HubInstallService
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromMinutes(5) };
    public static string HubDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CasualtiesHub");

    public async Task InstallAsync(HubRelease release, string installDirectory, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installDirectory)) throw new InvalidOperationException("Choose an install folder first.");
        var fullInstallDirectory = Path.GetFullPath(installDirectory);
        if (Path.GetPathRoot(fullInstallDirectory)?.TrimEnd(Path.DirectorySeparatorChar) == fullInstallDirectory.TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Choose a folder inside a drive, not the drive itself.");

        var temporaryRoot = Path.Combine(Path.GetTempPath(), "CasualtiesHubInstaller", Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(temporaryRoot, release.PackageName);
        var extractedPath = Path.Combine(temporaryRoot, "extracted");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            progress?.Report("Downloading the selected Hub release...");
            await using (var source = await _client.GetStreamAsync(release.PackageUrl, cancellationToken))
            await using (var destination = File.Create(zipPath))
                await source.CopyToAsync(destination, cancellationToken);

            if (!string.IsNullOrWhiteSpace(release.Sha256))
            {
                progress?.Report("Verifying the downloaded package...");
                await using var file = File.OpenRead(zipPath);
                var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
                if (!actualHash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The download checksum did not match the official release. Nothing was installed.");
            }

            progress?.Report("Extracting Casualties Hub...");
            ZipFile.ExtractToDirectory(zipPath, extractedPath, true);
            var executable = Directory.EnumerateFiles(extractedPath, "Casualties Hub.exe", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(extractedPath, "CasualtiesHub.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (executable is null) throw new InvalidOperationException("The release ZIP does not contain Casualties Hub.exe.");

            var packageRoot = Path.GetDirectoryName(executable)!;
            Directory.CreateDirectory(fullInstallDirectory);
            await StopHubProcessesInDirectoryAsync(fullInstallDirectory, cancellationToken);
            progress?.Report("Installing release files...");
            CopyDirectory(packageRoot, fullInstallDirectory);
            InstalledHubRegistry.Register(fullInstallDirectory, release.Tag);
            progress?.Report("Installation completed.");
        }
        finally
        {
            try { if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }
            catch { /* Temporary files are safe to leave if another process briefly holds them. */ }
        }
    }

    public static void Launch(string installDirectory)
    {
        var executable = new[] { "Casualties Hub.exe", "CasualtiesHub.exe" }
            .Select(file => Path.Combine(installDirectory, file))
            .FirstOrDefault(File.Exists);
        if (executable is null) throw new FileNotFoundException("Casualties Hub.exe was not found after installation.");
        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
    }

    /// <summary>Safely removes one confirmed Hub install folder without touching shared user data.</summary>
    public async Task UninstallAsync(string installDirectory, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installDirectory)) throw new InvalidOperationException("Choose a Casualties Hub installation first.");
        var fullInstallDirectory = Path.GetFullPath(installDirectory);
        if (!InstalledHubRegistry.IsHubInstallation(fullInstallDirectory))
            throw new InvalidOperationException("The selected folder does not contain Casualties Hub.exe, so the Setup Wizard will not delete it.");
        if (Path.GetPathRoot(fullInstallDirectory)?.TrimEnd(Path.DirectorySeparatorChar) == fullInstallDirectory.TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("The Setup Wizard will not delete a drive root.");

        progress?.Report("Closing Casualties Hub processes in the selected folder...");
        await StopHubProcessesInDirectoryAsync(fullInstallDirectory, cancellationToken);
        progress?.Report("Removing the selected Casualties Hub copy...");
        Directory.Delete(fullInstallDirectory, true);
        InstalledHubRegistry.Unregister(fullInstallDirectory);
        progress?.Report("Removal completed.");
    }

    /// <summary>Removes only the Hub-owned LocalAppData directory after the user explicitly chose full removal.</summary>
    public Task RemoveHubDataAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Removing Hub settings, cache, logs, and Protected Assets...");
        if (Directory.Exists(HubDataDirectory)) Directory.Delete(HubDataDirectory, true);
        progress?.Report("Hub data removal completed.");
        return Task.CompletedTask;
    }

    public static bool IsInsideHubDataDirectory(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(HubDataDirectory)) + Path.DirectorySeparatorChar;
        var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var destinationFile = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, true);
        }
    }

    private static async Task StopHubProcessesInDirectoryAsync(string installDirectory, CancellationToken cancellationToken)
    {
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installDirectory)) + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var executablePath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executablePath)) continue;
                var normalizedExecutable = Path.GetFullPath(executablePath);
                var isHubExecutable = Path.GetFileName(normalizedExecutable).Equals("Casualties Hub.exe", StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(normalizedExecutable).Equals("CasualtiesHub.exe", StringComparison.OrdinalIgnoreCase);
                if (!isHubExecutable || !normalizedExecutable.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase)) continue;

                process.Kill(true);
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            finally { process.Dispose(); }
        }
    }
}
