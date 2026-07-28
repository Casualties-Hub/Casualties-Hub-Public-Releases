using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Casualties_Hub_Installer.Models;

namespace Casualties_Hub_Installer.Services;

/// <summary>Reads official Hub releases only; no GitHub token is embedded in the installer.</summary>
public sealed class GitHubReleaseService
{
    private const string Repository = "MarlyZ89/Casualties-Hub-Public-Release";
    // GitHub occasionally delays a newly published release appearing in its
    // release-list response, even though /releases/tags/{tag} already works.
    // Checking only the newest few version tags keeps the installer reliable
    // without turning one refresh into dozens of API requests.
    private const int RecentTagFallbackLimit = 4;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<IReadOnlyList<HubRelease>> GetReleasesAsync(bool includePrereleases, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases?per_page=100");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CasualtiesHubInstaller", "0.0.1"));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var releasesByTag = new Dictionary<string, HubRelease>(StringComparer.OrdinalIgnoreCase);
        foreach (var release in document.RootElement.EnumerateArray())
        {
            var parsed = await ParseReleaseAsync(release, includePrereleases, cancellationToken);
            if (parsed is not null)
                releasesByTag[parsed.Tag] = parsed;
        }

        foreach (var tag in (await GetRecentVersionTagsAsync(cancellationToken))
                     .Where(tag => !releasesByTag.ContainsKey(tag))
                     .Take(RecentTagFallbackLimit))
        {
            var release = await GetReleaseByTagAsync(tag, cancellationToken);
            if (release is null) continue;

            var parsed = await ParseReleaseAsync(release.Value, includePrereleases, cancellationToken);
            if (parsed is not null)
                releasesByTag[parsed.Tag] = parsed;
        }

        return releasesByTag.Values
            .OrderByDescending(release => release.Version)
            .ThenByDescending(release => release.IsPrerelease)
            .ToList();
    }

    private async Task<HubRelease?> ParseReleaseAsync(JsonElement release, bool includePrereleases, CancellationToken cancellationToken)
    {
        if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        var tag = release.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
        if (!TryParseVersion(tag, out var version, out var prereleaseLabel)) return null;
        var isPrerelease = release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean();
        if (isPrerelease != (prereleaseLabel is not null)) return null;
        if (isPrerelease && !includePrereleases) return null;

        var package = FindZipAsset(release);
        if (package is null) return null;
        var pageUrl = release.TryGetProperty("html_url", out var pageValue) ? pageValue.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(pageUrl)) return null;
        var checksum = package.Value.Sha256 ?? await GetChecksumAsync(release, package.Value.Name, cancellationToken);
        return new HubRelease(version, tag!, isPrerelease, pageUrl, package.Value.Url, package.Value.Name, checksum);
    }

    private async Task<IReadOnlyList<string>> GetRecentVersionTagsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/tags?per_page={RecentTagFallbackLimit}");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CasualtiesHubInstaller", "0.0.1"));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return Array.Empty<string>();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (document.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return document.RootElement.EnumerateArray()
            .Select(tag => tag.TryGetProperty("name", out var name) ? name.GetString() : null)
            .Where(tag => TryParseVersion(tag, out _, out _))
            .OfType<string>()
            .ToList();
    }

    private async Task<JsonElement?> GetReleaseByTagAsync(string tag, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/tags/{Uri.EscapeDataString(tag)}");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CasualtiesHubInstaller", "0.0.1"));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.Clone();
    }

    private static (string Name, string Url, string? Sha256)? FindZipAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? "" : "";
            var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(url)) continue;
            var digest = asset.TryGetProperty("digest", out var digestValue) ? digestValue.GetString() : null;
            if (digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true) digest = digest["sha256:".Length..];
            return (name, url, digest);
        }
        return null;
    }

    private async Task<string?> GetChecksumAsync(JsonElement release, string packageName, CancellationToken cancellationToken)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? "" : "";
            if (!name.Equals(packageName + ".sha256.txt", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) continue;
            var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(url)) continue;
            var text = await _client.GetStringAsync(url, cancellationToken);
            var match = Regex.Match(text, "(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])");
            if (match.Success) return match.Value;
        }
        return null;
    }

    private static bool TryParseVersion(string? tag, out Version version, out string? prerelease)
    {
        version = new Version();
        prerelease = null;
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var clean = tag.Trim().TrimStart('v', 'V');
        var parts = clean.Split('-', 2);
        if (!Version.TryParse(parts[0], out var parsedVersion) || parsedVersion is null) return false;
        version = parsedVersion;
        prerelease = parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null;
        return true;
    }
}
