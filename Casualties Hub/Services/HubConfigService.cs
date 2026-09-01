using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Reads the published Casualties Hub configuration with a local cache and HTTP
/// validators. A prerelease build reads the prerelease channel, every other
/// build reads stable.
/// </summary>
public sealed class HubConfigService
{
    public const string BaseUrl = "https://casualties-hub.github.io/Casualties-Hub-Config/";
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private readonly string _cachePath;
    private readonly string _statePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public HubConfigService(SettingsService settingsService)
        : this(settingsService, ChannelFor(HubVersion.Current()))
    {
    }

    public HubConfigService(SettingsService settingsService, HubConfigChannel channel)
    {
        Channel = channel;
        // The cache is named per channel so moving between a prerelease and a
        // public build never reads the other channel's document.
        _cachePath = Path.Combine(settingsService.AppDataPath, $"HubConfigCache.{channel}.json");
        _statePath = Path.Combine(settingsService.AppDataPath, $"HubConfigHttpState.{channel}.json");
    }

    public HubConfigChannel Channel { get; }

    public string ConfigUrl => BaseUrl + (Channel == HubConfigChannel.Prerelease ? "v1/prerelease/hub.json" : "v1/hub.json");

    public static HubConfigChannel ChannelFor(HubVersion version)
        => version.IsPrerelease ? HubConfigChannel.Prerelease : HubConfigChannel.Stable;

    public HubConfigResult LoadCached()
    {
        var state = LoadState();
        return new HubConfigResult(LoadBestAvailable(), false, true, false, state.LastCheckedUtc?.Add(RefreshInterval));
    }

    public bool IsCheckDue() => LoadState().LastCheckedUtc is not { } last || DateTimeOffset.UtcNow - last >= RefreshInterval;

    public async Task<HubConfigResult> RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var state = LoadState();
            if (!force && state.LastCheckedUtc is { } last && DateTimeOffset.UtcNow - last < RefreshInterval)
                return new HubConfigResult(LoadBestAvailable(), false, true, false, state.LastCheckedUtc?.Add(RefreshInterval));

            using var request = new HttpRequestMessage(HttpMethod.Get, ConfigUrl);
            if (!string.IsNullOrWhiteSpace(state.ETag)) request.Headers.TryAddWithoutValidation("If-None-Match", state.ETag);
            if (state.LastModifiedUtc is { } modified) request.Headers.IfModifiedSince = modified;

            try
            {
                using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var checkedAtUtc = DateTimeOffset.UtcNow;
                state.LastCheckedUtc = checkedAtUtc;

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    SaveState(state);
                    DebugLogService.Activity("Hub configuration", $"The {Channel} configuration is unchanged; kept the local cache.");
                    return new HubConfigResult(LoadBestAvailable(), true, true, false, checkedAtUtc.Add(RefreshInterval));
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var config = Deserialize(json);
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                File.WriteAllText(_cachePath, json);
                state.ETag = response.Headers.ETag?.ToString();
                state.LastModifiedUtc = response.Content.Headers.LastModified;
                SaveState(state);
                DebugLogService.Activity("Hub configuration", $"Downloaded an updated {Channel} configuration.");
                return new HubConfigResult(config, true, false, true, checkedAtUtc.Add(RefreshInterval));
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                var checkedAtUtc = DateTimeOffset.UtcNow;
                state.LastCheckedUtc = checkedAtUtc;
                SaveState(state);
                DebugLogService.Error("Hub configuration refresh failed; using the cached copy", exception);
                return new HubConfigResult(LoadBestAvailable(), false, true, false, checkedAtUtc.Add(RefreshInterval));
            }
        }
        finally { RefreshLock.Release(); }
    }

    private HubConfig LoadBestAvailable()
    {
        if (!File.Exists(_cachePath)) return new HubConfig();
        try { return Deserialize(File.ReadAllText(_cachePath)); }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // A build that has never been online, or whose cache is damaged, falls
            // back to the links compiled into it rather than showing nothing.
            DebugLogService.Error($"Could not load {Path.GetFileName(_cachePath)}", exception);
            return new HubConfig();
        }
    }

    private HubConfig Deserialize(string json)
    {
        var config = JsonSerializer.Deserialize<HubConfig>(json, _jsonOptions) ?? throw new JsonException("The Hub configuration was empty.");

        config.Links.DiscordUrl = RequireHttpsUrl(config.Links.DiscordUrl, "discordUrl");
        config.Links.ReportUrl = RequireHttpsUrl(config.Links.ReportUrl, "reportUrl");

        if (config.CurrentAnnouncement is { } current && !IsPublishable(current))
            throw new JsonException("The current announcement was missing an id or a message.");

        config.PreviousAnnouncements ??= [];
        config.PreviousAnnouncements.RemoveAll(announcement => !IsPublishable(announcement));
        return config;
    }

    private static bool IsPublishable(HubAnnouncement announcement)
        => !string.IsNullOrWhiteSpace(announcement.Id) && !string.IsNullOrWhiteSpace(announcement.Message);

    /// <summary>
    /// The Hub hands these straight to the shell, so a published link that is not
    /// plain https is rejected along with the document that carried it.
    /// </summary>
    private static string RequireHttpsUrl(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
            throw new JsonException($"The configuration field {field} was not an https URL.");
        return uri.AbsoluteUri;
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
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CasualtiesHub", HubVersion.Current().ToString()));
        return client;
    }

    private sealed class CacheState
    {
        public string? ETag { get; set; }
        public DateTimeOffset? LastModifiedUtc { get; set; }
        public DateTimeOffset? LastCheckedUtc { get; set; }
    }
}
