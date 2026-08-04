using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>Loads public Hub Home content from GitHub with a local cache and HTTP validators.</summary>
public sealed class GitHubHubContentService
{
    public const string ContentUrl = "https://raw.githubusercontent.com/MarlyZ89/Casualties-Hub-Public-Releases/main/HubContent.json";
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);
    // The manual button gets its own much shorter cooldown. This still goes
    // through the conditional If-None-Match/If-Modified-Since request below, so
    // checking often does not re-download HubContent.json when it is unchanged.
    public static readonly TimeSpan ManualCheckInterval = TimeSpan.FromSeconds(30);
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private readonly string _cachePath;
    private readonly string _statePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public GitHubHubContentService(SettingsService settingsService)
    {
        _cachePath = Path.Combine(settingsService.AppDataPath, "HubContentCache.json");
        _statePath = Path.Combine(settingsService.AppDataPath, "HubContentHttpState.json");
    }

    public HubContentResult LoadCached()
    {
        var state = LoadState();
        return new HubContentResult(LoadBestAvailable(), false, true, false, state.LastCheckedUtc?.Add(RefreshInterval));
    }

    public bool IsCheckDue() => LoadState().LastCheckedUtc is not { } last || DateTimeOffset.UtcNow - last >= RefreshInterval;

    /// <summary>Whether the "Check now" button's own, much shorter cooldown has elapsed.</summary>
    public bool IsManualCheckDue() => LoadState().LastManualCheckUtc is not { } last || DateTimeOffset.UtcNow - last >= ManualCheckInterval;

    public DateTimeOffset? NextManualCheckUtc() => LoadState().LastManualCheckUtc?.Add(ManualCheckInterval);

    /// <param name="manual">
    /// Gates the request against <see cref="ManualCheckInterval"/> instead of
    /// <see cref="RefreshInterval"/>, and records the attempt separately so the
    /// manual button's cooldown never depends on the automatic timer.
    /// </param>
    public async Task<HubContentResult> RefreshAsync(bool force = false, bool manual = false, CancellationToken cancellationToken = default)
    {
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var state = LoadState();
            var gateLast = manual ? state.LastManualCheckUtc : state.LastCheckedUtc;
            var gateInterval = manual ? ManualCheckInterval : RefreshInterval;
            if (!force && gateLast is { } last && DateTimeOffset.UtcNow - last < gateInterval)
                return new HubContentResult(LoadBestAvailable(), false, true, false, state.LastCheckedUtc?.Add(RefreshInterval));

            using var request = new HttpRequestMessage(HttpMethod.Get, ContentUrl);
            if (!string.IsNullOrWhiteSpace(state.ETag)) request.Headers.TryAddWithoutValidation("If-None-Match", state.ETag);
            if (state.LastModifiedUtc is { } modified) request.Headers.IfModifiedSince = modified;

            try
            {
                using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var checkedAtUtc = DateTimeOffset.UtcNow;
                state.LastCheckedUtc = checkedAtUtc;
                if (manual) state.LastManualCheckUtc = checkedAtUtc;
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    SaveState(state);
                    DebugLogService.Activity("GitHub content", "HubContent.json is unchanged; kept the local cache.");
                    return new HubContentResult(LoadBestAvailable(), true, true, false, checkedAtUtc.Add(RefreshInterval));
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var content = Deserialize(json);
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                File.WriteAllText(_cachePath, json);
                state.ETag = response.Headers.ETag?.ToString();
                state.LastModifiedUtc = response.Content.Headers.LastModified;
                SaveState(state);
                DebugLogService.Activity("GitHub content", "Downloaded an updated HubContent.json.");
                return new HubContentResult(content, true, false, true, checkedAtUtc.Add(RefreshInterval));
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                var checkedAtUtc = DateTimeOffset.UtcNow;
                state.LastCheckedUtc = checkedAtUtc;
                if (manual) state.LastManualCheckUtc = checkedAtUtc;
                SaveState(state);
                DebugLogService.Error("GitHub Hub content refresh failed; using cached content", exception);
                return new HubContentResult(LoadBestAvailable(), false, true, false, checkedAtUtc.Add(RefreshInterval));
            }
        }
        finally { RefreshLock.Release(); }
    }

    private HubContent LoadBestAvailable()
    {
        if (File.Exists(_cachePath))
        {
            try { return Deserialize(File.ReadAllText(_cachePath)); }
            catch (Exception exception) when (exception is IOException or JsonException)
            { DebugLogService.Error($"Could not load {Path.GetFileName(_cachePath)}", exception); }
        }

        // Falls back to the copy bundled in the EXE, or beside it on an unpacked install.
        if (BundledData.Read("Bundled/HubContent.json", Path.Combine("Data", "HubContent.json")) is { } bundled)
        {
            try { return Deserialize(bundled.Text); }
            catch (JsonException exception)
            { DebugLogService.Error("Could not load the bundled HubContent.json", exception); }
        }
        return new HubContent();
    }

    private HubContent Deserialize(string json)
    {
        var content = JsonSerializer.Deserialize<HubContent>(json, _jsonOptions) ?? throw new JsonException("Hub content was empty.");
        if (content.SchemaVersion != 1) throw new JsonException($"Unsupported Hub content schema {content.SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(content.CurrentAnnouncement.Message)) throw new JsonException("Current announcement was empty.");
        content.PreviousAnnouncements ??= [];
        return content;
    }

    private CacheState LoadState()
    {
        try { return File.Exists(_statePath) ? JsonSerializer.Deserialize<CacheState>(File.ReadAllText(_statePath), _jsonOptions) ?? new() : new(); }
        catch { return new(); }
    }

    private void SaveState(CacheState state) => File.WriteAllText(_statePath, JsonSerializer.Serialize(state, _jsonOptions));

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CasualtiesHub", "0.0.8-pre.3"));
        return client;
    }

    private sealed class CacheState
    {
        public string? ETag { get; set; }
        public DateTimeOffset? LastModifiedUtc { get; set; }
        public DateTimeOffset? LastCheckedUtc { get; set; }
        public DateTimeOffset? LastManualCheckUtc { get; set; }
    }
}
