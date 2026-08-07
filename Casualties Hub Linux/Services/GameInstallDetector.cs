using System.IO;

namespace Casualties_Hub.Services;

/// <summary>
/// Locates the Casualties Unknown install on Linux.
/// </summary>
/// <remarks>
/// The Windows Hub walks drive letters C: to H: looking for "Program Files\Steam\steamapps\...".
/// On Linux every one of those Directory.Exists checks is false, so detection returned null every
/// time without raising an error: the Hub simply behaved as though the game were not installed.
/// This asks Steam where its libraries are instead.
/// </remarks>
public sealed class GameInstallDetector
{
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

    /// <summary>The full Steam record, which carries the app id needed to launch through steam://.</summary>
    public async Task<SteamGameInstall?> FindSteamInstallAsync(TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            return await Task.Run(SteamLibraryLocator.FindCasualtiesUnknown, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static string? FindGameInstall(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var steamInstall = SteamLibraryLocator.FindCasualtiesUnknown();
        if (steamInstall is not null)
        {
            DebugLogService.Activity("Game detection", $"Found via Steam manifest (appid {steamInstall.AppId}): {steamInstall.Path}");
            return steamInstall.Path;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Steam knows nothing about it. Fall back to a shallow sweep of the common/ folders,
        // which still catches a hand-copied install that has no manifest.
        foreach (var library in SteamLibraryLocator.FindLibraries())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var common = LinuxPaths.ResolveChain(library, "steamapps", "common");
            if (!Directory.Exists(common)) continue;

            try
            {
                var match = Directory.EnumerateDirectories(common)
                    .FirstOrDefault(directory => Path.GetFileName(directory)
                        .Contains("Casualties", StringComparison.OrdinalIgnoreCase));

                if (match is not null)
                {
                    DebugLogService.Activity("Game detection", $"Found by folder name (no Steam manifest): {match}");
                    return match;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Unreadable library; try the next.
            }
        }

        DebugLogService.Info("Game detection found no Casualties Unknown install.");
        return null;
    }
}
