using System.IO;

namespace Casualties_Hub.Services;

/// <summary>
/// Mirrors the standard Steam paths used by CCLScavTemplate vars.targets.
/// The scan is bounded so a missing or disconnected drive never holds up the UI.
/// </summary>
public sealed class GameInstallDetector
{
    private const string GameFolderName = "Casualties Unknown Demo";

    public async Task<string?> FindGameInstallAsync(TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            return await Task.Run(() => FindGameInstall(cancellation.Token), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static string? FindGameInstall(CancellationToken cancellationToken)
    {
        foreach (var driveLetter in new[] { 'C', 'D', 'E', 'F', 'G', 'H' })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var drive = driveLetter + @":\";
            if (!Directory.Exists(drive)) continue;

            foreach (var steamPath in new[]
            {
                Path.Combine(drive, "Program Files (x86)", "Steam", "steamapps", "common", GameFolderName),
                Path.Combine(drive, "Program Files", "Steam", "steamapps", "common", GameFolderName),
                Path.Combine(drive, "SteamLibrary", "steamapps", "common", GameFolderName),
                Path.Combine(drive, "Steam", "steamapps", "common", GameFolderName)
            })
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(steamPath)) return steamPath;
            }
        }
        return null;
    }
}
