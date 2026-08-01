using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.IO;
using System.Net;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>Finds the newest channel-eligible ZIP release from the official public repository.</summary>
public sealed class GitHubUpdateService
{
    private const string OfficialRepository = "MarlyZ89/Casualties-Hub-Public-Releases";
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly string _cachePath;
    private readonly string _statePath;

    public GitHubUpdateService(SettingsService settingsService)
    {
        _cachePath = Path.Combine(settingsService.AppDataPath, "GitHubReleasesCache.json");
        _statePath = Path.Combine(settingsService.AppDataPath, "GitHubReleasesHttpState.json");
    }

    public async Task<GitHubUpdate?> CheckForUpdateAsync(HubVersion currentVersion, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{OfficialRepository}/releases?per_page=100");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("CasualtiesHub", currentVersion.ToString()));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        var state = LoadState();
        if (!string.IsNullOrWhiteSpace(state.ETag)) request.Headers.TryAddWithoutValidation("If-None-Match", state.ETag);
        if (state.LastModifiedUtc is { } modified) request.Headers.IfModifiedSince = modified;
        using var response = await _client.SendAsync(request, cancellationToken);
        string json;
        if (response.StatusCode == HttpStatusCode.NotModified && File.Exists(_cachePath))
        {
            json = await File.ReadAllTextAsync(_cachePath, cancellationToken);
            DebugLogService.Activity("Update check", "GitHub releases are unchanged; used the local release cache.");
        }
        else
        {
            if (!response.IsSuccessStatusCode) return null;
            json = await response.Content.ReadAsStringAsync(cancellationToken);
            await File.WriteAllTextAsync(_cachePath, json, cancellationToken);
            state.ETag = response.Headers.ETag?.ToString();
            state.LastModifiedUtc = response.Content.Headers.LastModified;
            SaveState(state);
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        GitHubUpdate? selected = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            var tag = release.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
            if (!HubVersion.TryParse(tag, out var candidateVersion)) continue;

            var isPrerelease = release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean();
            // Final tags are authoritative even if GitHub's checkbox was missed;
            // prerelease tags must remain prerelease-only for stable installations.
            if (isPrerelease != candidateVersion.IsPrerelease) continue;
            if (!IsEligible(currentVersion, candidateVersion)) continue;

            var package = FindZipAsset(release);
            if (package is null) continue;
            var digest = package.Value.Digest ?? await GetChecksumFromReleaseAsync(release, package.Value.Name, cancellationToken);
            if (string.IsNullOrWhiteSpace(digest)) continue;
            var releaseUrl = release.TryGetProperty("html_url", out var page) ? page.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(releaseUrl)) continue;

            var candidate = new GitHubUpdate(candidateVersion, releaseUrl, package.Value.Url, digest);
            if (selected is null || candidate.Version.CompareTo(selected.Version) > 0)
                selected = candidate;
        }
        return selected;
    }

    private static bool IsEligible(HubVersion current, HubVersion candidate)
    {
        if (candidate.CompareTo(current) <= 0) return false;
        // Stable installations never cross into a prerelease channel. Pre-release
        // testers can take a newer prerelease or the finished stable build.
        return current.IsPrerelease || !candidate.IsPrerelease;
    }

    private static (string Name, string Url, string? Digest)? FindZipAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? "" : "";
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(url)) continue;
            var digest = asset.TryGetProperty("digest", out var digestValue) ? digestValue.GetString() : null;
            if (digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true)
                digest = digest["sha256:".Length..];
            return (name, url, digest);
        }
        return null;
    }

    private async Task<string?> GetChecksumFromReleaseAsync(JsonElement release, string packageName, CancellationToken cancellationToken)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? "" : "";
            var expectedName = packageName + ".sha256.txt";
            if (!name.Equals(expectedName, StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) continue;
            var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(url)) continue;
            var checksumText = await _client.GetStringAsync(url, cancellationToken);
            var match = System.Text.RegularExpressions.Regex.Match(checksumText, "(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])");
            if (match.Success) return match.Value;
        }
        return null;
    }

    private CacheState LoadState()
    {
        try { return File.Exists(_statePath) ? JsonSerializer.Deserialize<CacheState>(File.ReadAllText(_statePath)) ?? new() : new(); }
        catch { return new(); }
    }

    private void SaveState(CacheState state) => File.WriteAllText(_statePath, JsonSerializer.Serialize(state));

    private sealed class CacheState
    {
        public string? ETag { get; set; }
        public DateTimeOffset? LastModifiedUtc { get; set; }
    }
}

public sealed record GitHubUpdate(HubVersion Version, string ReleaseUrl, string PackageUrl, string? Sha256);
