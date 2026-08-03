using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Retrieves the current community metadata list on every dashboard startup.
/// A local cache exists only as an offline fallback if GitHub cannot be reached.
/// </summary>
public sealed class UniversalMetadataService
{
    public const string MetadataUrl = "https://github.com/jimmyking9999999/Metadata-generator/raw/refs/heads/main/nexusmods.json";
    // Moderators can mark a mod adult at any time, so a cache is only trusted briefly.
    // Beyond this age the live list is requested and the cache serves as an offline fallback.
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);
    private static readonly HttpClient HttpClient = CreateClient();
    private readonly string _cachePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static IReadOnlyList<MetadataMod> _lastSuccessfulMods = [];

    public static IReadOnlyList<MetadataMod> LastSuccessfulMods => _lastSuccessfulMods;

    public UniversalMetadataService(SettingsService settingsService)
    {
        _cachePath = Path.Combine(settingsService.AppDataPath, "UniversalMetadataCache.json");
    }

    public async Task<IReadOnlyList<MetadataMod>> GetModsAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && File.Exists(_cachePath) && !IsCacheStale())
        {
            try
            {
                var cachedMods = Deserialize(File.ReadAllText(_cachePath));
                _lastSuccessfulMods = cachedMods;
                DebugLogService.Info($"Using cached universal metadata: {cachedMods.Count} mods.");
                return cachedMods;
            }
            catch (JsonException exception)
            {
                DebugLogService.Error("Cached metadata was invalid; requesting a fresh copy.", exception);
            }
        }
        try
        {
            using var response = await HttpClient.GetAsync(MetadataUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var mods = Deserialize(json);
            File.WriteAllText(_cachePath, json);
            _lastSuccessfulMods = mods;
            DebugLogService.Info($"Live universal metadata loaded: {mods.Count} mods.");
            return mods;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            DebugLogService.Error("Live universal metadata request failed", exception);
            if (!File.Exists(_cachePath))
                throw new InvalidOperationException("The live metadata request failed and no offline cache is available.", exception);

            var cachedMods = Deserialize(File.ReadAllText(_cachePath));
            _lastSuccessfulMods = cachedMods;
            DebugLogService.Info($"Using offline universal metadata cache: {cachedMods.Count} mods.");
            return cachedMods;
        }
    }

    private bool IsCacheStale()
    {
        try
        {
            return DateTime.UtcNow - File.GetLastWriteTimeUtc(_cachePath) > CacheLifetime;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private List<MetadataMod> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<MetadataMod>>(json, _jsonOptions) ?? [];

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CasualtiesHub", "0.0.5-pre.1"));
        return client;
    }
}
