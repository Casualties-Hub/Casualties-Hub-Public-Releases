using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.IO;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Read-only Nexus API client. It only fetches public metadata and opens the
/// original Nexus page; it never downloads or redistributes mod files.
/// </summary>
public sealed class NexusService
{
    private const string ApiUrl = "https://api.nexusmods.com/v2/graphql";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(8);
    private readonly string _cachePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public NexusService(SettingsService settingsService)
    {
        _cachePath = Path.Combine(settingsService.AppDataPath, "NexusPopularMods.json");
    }

    public async Task<IReadOnlyList<NexusMod>> GetPopularModsAsync(string apiKey, bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Add your personal Nexus API key in Settings before loading the Nexus list.");

        if (!forceRefresh && TryLoadCache(out var cachedMods))
            return cachedMods;

        const string query = """
            query {
              mods(
                filter: { gameDomainName: [{ value: "scavprototype" }] }
                sort: [{ downloads: { direction: DESC } }]
                offset: 0
                count: 50
              ) {
                nodes {
                  modId
                  name
                  author
                  version
                  downloads
                  thumbnailUrl
                }
              }
            }
            """;

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Add("apikey", apiKey);
        request.Headers.Add("Application-Name", "CasualtiesHub");
        request.Headers.Add("Application-Version", "0.0.8-pre.6");
        request.Content = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nexus returned {(int)response.StatusCode}. Check your API key and try again.");

        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("errors", out var errors))
            throw new InvalidOperationException($"Nexus could not load the list: {errors[0].GetProperty("message").GetString()}");

        var nodes = document.RootElement
            .GetProperty("data")
            .GetProperty("mods")
            .GetProperty("nodes");

        var mods = new List<NexusMod>();
        foreach (var node in nodes.EnumerateArray())
        {
            mods.Add(new NexusMod
            {
                ModId = node.GetProperty("modId").GetInt32(),
                Name = GetString(node, "name", "Unknown mod"),
                Author = GetString(node, "author", "Unknown author"),
                Version = GetString(node, "version", "Unknown"),
                Downloads = node.GetProperty("downloads").GetInt32(),
                ThumbnailUrl = GetNullableString(node, "thumbnailUrl")
            });
        }

        File.WriteAllText(_cachePath, JsonSerializer.Serialize(mods, _jsonOptions));
        return mods;
    }

    private bool TryLoadCache(out IReadOnlyList<NexusMod> mods)
    {
        mods = Array.Empty<NexusMod>();
        if (!File.Exists(_cachePath) || DateTime.UtcNow - File.GetLastWriteTimeUtc(_cachePath) > CacheLifetime)
            return false;

        try
        {
            mods = JsonSerializer.Deserialize<List<NexusMod>>(File.ReadAllText(_cachePath), _jsonOptions) ?? [];
            return mods.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GetString(JsonElement element, string property, string fallback) =>
        GetNullableString(element, property) ?? fallback;

    private static string? GetNullableString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
}
