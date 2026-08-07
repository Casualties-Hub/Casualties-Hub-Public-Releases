using System.Collections.Concurrent;
using System.IO;
using Avalonia.Media.Imaging;

namespace Casualties_Hub.Services;

/// <summary>
/// Fetches mod icons over HTTP and keeps them on disk between runs.
/// </summary>
/// <remarks>
/// WPF's Image control downloads an http(s) URI assigned to Source by itself. Avalonia's does not:
/// binding a URL string there silently yields no image, which is why the dashboard cards need this.
/// Icons are cached under the Hub data folder so a second visit to the page costs nothing, and a
/// failed fetch is remembered for the session so a broken URL is not retried on every scroll.
/// </remarks>
public static class RemoteImageCache
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly ConcurrentDictionary<string, Bitmap?> Memory = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim Gate = new(4); // Be polite to the image host.

    /// <summary>Returns the icon, or null if it cannot be fetched. Never throws.</summary>
    public static async Task<Bitmap?> GetAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Memory.TryGetValue(url, out var cached)) return cached;

        // Only ever fetch over http(s). Metadata is remote input, so a file:// or other scheme
        // slipping through here would have this reading arbitrary local paths.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Memory[url] = null;
            return null;
        }

        var diskPath = CachePath(url);
        if (File.Exists(diskPath))
        {
            var fromDisk = TryLoad(diskPath);
            Memory[url] = fromDisk;
            if (fromDisk is not null) return fromDisk;
        }

        await Gate.WaitAsync();
        try
        {
            if (Memory.TryGetValue(url, out var raced) && raced is not null) return raced;

            var bytes = await Client.GetByteArrayAsync(uri);
            Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);
            await File.WriteAllBytesAsync(diskPath, bytes);

            var bitmap = TryLoad(diskPath);
            Memory[url] = bitmap;
            return bitmap;
        }
        catch (Exception exception)
        {
            // A missing icon is cosmetic. Remember the failure so the page does not retry it
            // on every redraw, and keep the card usable.
            DebugLogService.Info($"Mod icon could not be fetched ({exception.GetType().Name}): {url}");
            Memory[url] = null;
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static Bitmap? TryLoad(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception exception)
        {
            DebugLogService.Info($"Cached icon could not be decoded: {exception.Message}");
            try { File.Delete(path); } catch { /* best effort */ }
            return null;
        }
    }

    /// <summary>A stable filename per URL, hashed so remote text never becomes a path.</summary>
    private static string CachePath(string url)
    {
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url)))[..32];
        return Path.Combine(LinuxPaths.AppDataRoot(), "IconCache", hash + ".img");
    }
}
