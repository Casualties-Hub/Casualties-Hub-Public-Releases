using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace Casualties_Hub.Services;

/// <summary>Downloads a verified GitHub release and hands file replacement to a tiny temporary CMD helper.</summary>
public sealed class UpdateInstaller
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromMinutes(4) };

    public async Task<string> DownloadAndStartAsync(GitHubUpdate update, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.PackageUrl))
            throw new InvalidOperationException("This GitHub release has no ZIP update package attached.");

        var stagingDirectory = Path.Combine(Path.GetTempPath(), "CasualtiesHub", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        var archivePath = Path.Combine(stagingDirectory, "CasualtiesHubUpdate.zip");
        DebugLogService.Activity("Updater", $"Downloading update {update.Version} from GitHub.");
        using (var response = await _client.GetAsync(update.PackageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(archivePath);
            await input.CopyToAsync(output, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(update.Sha256))
        {
            await using var stream = File.OpenRead(archivePath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!actual.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The downloaded update failed its SHA-256 integrity check and was not installed.");
        }
        else
        {
            throw new InvalidOperationException("This release has no SHA-256 digest. Upload the release ZIP through GitHub Releases so GitHub provides an asset digest.");
        }

        VerifyHubArchive(archivePath);
        return StartReplacement(archivePath, stagingDirectory);
    }

    /// <summary>Installs a release ZIP the player previously downloaded locally.</summary>
    public Task<string> InstallLocalArchiveAndStartAsync(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            throw new FileNotFoundException("The selected Hub release ZIP could not be found.", archivePath);

        VerifyHubArchive(archivePath);
        var stagingDirectory = Path.Combine(Path.GetTempPath(), "CasualtiesHub", "LocalUpdates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        DebugLogService.Activity("Updater", $"Preparing local Hub release package: {Path.GetFileName(archivePath)}.");
        return Task.FromResult(StartReplacement(archivePath, stagingDirectory));
    }

    private static void VerifyHubArchive(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (!archive.Entries.Any(entry => Path.GetFileName(entry.FullName).Equals("Casualties Hub.exe", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The selected ZIP does not contain Casualties Hub.exe.");
    }

    private static string StartReplacement(string archivePath, string stagingDirectory)
    {
        var targetDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var scriptPath = Path.Combine(stagingDirectory, "Apply-CasualtiesHub-Update.cmd");
        File.WriteAllText(scriptPath, BuildUpdateScript(archivePath, stagingDirectory, targetDirectory, Environment.ProcessId));
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"") { UseShellExecute = true, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        DebugLogService.Activity("Updater", "Verified Hub release package; replacement helper started.");
        return stagingDirectory;
    }

    private static string BuildUpdateScript(string archivePath, string stagingDirectory, string targetDirectory, int processId)
    {
        static string QuotePs(string path) => path.Replace("'", "''");
        var extractionPath = Path.Combine(stagingDirectory, "Extracted");
        var powershell = "$ErrorActionPreference='Stop'; " +
                         $"Expand-Archive -LiteralPath '{QuotePs(archivePath)}' -DestinationPath '{QuotePs(extractionPath)}' -Force; " +
                         $"$exe=Get-ChildItem -LiteralPath '{QuotePs(extractionPath)}' -Filter 'Casualties Hub.exe' -Recurse | Select-Object -First 1; " +
                         "if ($null -eq $exe) { throw 'Casualties Hub.exe not found in update package.' }; " +
                         "$source=$exe.Directory.FullName; " +
                         $"Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination '{QuotePs(targetDirectory)}' -Recurse -Force; " +
                         $"Start-Process -FilePath (Join-Path '{QuotePs(targetDirectory)}' 'Casualties Hub.exe')";

        return $"@echo off{Environment.NewLine}" +
               "setlocal EnableExtensions DisableDelayedExpansion" + Environment.NewLine +
               $"set PID={processId}{Environment.NewLine}" +
               ":wait" + Environment.NewLine +
               "tasklist /FI \"PID eq %PID%\" /NH | findstr /R /C:\"^.* %PID% .*\" >nul" + Environment.NewLine +
               "if not errorlevel 1 (timeout /t 1 /nobreak >nul & goto wait)" + Environment.NewLine +
               $"powershell -NoProfile -ExecutionPolicy Bypass -Command \"{powershell.Replace("\"", "\\\"")}\"" + Environment.NewLine +
               "endlocal" + Environment.NewLine +
               "del \"%~f0\"";
    }
}
