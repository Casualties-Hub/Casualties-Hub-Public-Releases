using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>Premium direct-download flow using the player's own Nexus API key.</summary>
public sealed class NexusDownloadService
{
    private const string GameDomain = "scavprototype";
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<string> DownloadLatestFileAsync(MetadataMod mod, string apiKey, string downloadFolder)
    {
        DebugLogService.Activity("Nexus Premium", $"Requesting the latest downloadable file for {mod.Name}.");
        if (string.IsNullOrWhiteSpace(mod.NexusUrl))
            throw new InvalidOperationException("This metadata entry does not include a Nexus mod page.");
        var idMatch = Regex.Match(mod.NexusUrl, @"/mods/(?<id>\d+)", RegexOptions.IgnoreCase);
        if (!idMatch.Success)
            throw new InvalidOperationException("Could not determine this mod's Nexus ID from its metadata link.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Add your personal Nexus Premium API key in Settings first.");

        var modId = idMatch.Groups["id"].Value;
        using var fileRequest = CreateRequest($"https://api.nexusmods.com/v1/games/{GameDomain}/mods/{modId}/files.json", apiKey);
        using var fileResponse = await _client.SendAsync(fileRequest);
        var fileJson = await fileResponse.Content.ReadAsStringAsync();
        if (!fileResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nexus could not list downloadable files ({(int)fileResponse.StatusCode}). Check your API key and Premium status.");

        using var fileDocument = JsonDocument.Parse(fileJson);
        var file = SelectLatestFile(fileDocument.RootElement);
        var fileId = GetInt(file, "file_id", "id");
        if (fileId <= 0) throw new InvalidOperationException("Nexus did not provide a downloadable file for this mod.");

        using var linkRequest = CreateRequest($"https://api.nexusmods.com/v1/games/{GameDomain}/mods/{modId}/files/{fileId}/download_link.json", apiKey);
        using var linkResponse = await _client.SendAsync(linkRequest);
        var linkJson = await linkResponse.Content.ReadAsStringAsync();
        if (!linkResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nexus refused the direct-download request ({(int)linkResponse.StatusCode}). The account may not be Premium or the API key may be invalid.");

        using var linksDocument = JsonDocument.Parse(linkJson);
        var downloadUrl = GetString(linksDocument.RootElement.ValueKind == JsonValueKind.Array
            ? linksDocument.RootElement.EnumerateArray().FirstOrDefault()
            : linksDocument.RootElement, "URI", "uri");
        if (string.IsNullOrWhiteSpace(downloadUrl)) throw new InvalidOperationException("Nexus did not return a download link.");

        Directory.CreateDirectory(downloadFolder);
        var fileName = MakeSafeFileName(GetString(file, "file_name", "name") ?? $"{mod.Name}_{fileId}.zip");
        var destination = Path.Combine(downloadFolder, fileName);
        using var downloadResponse = await _client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        downloadResponse.EnsureSuccessStatusCode();
        await using var input = await downloadResponse.Content.ReadAsStreamAsync();
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output);
        DebugLogService.Activity("Nexus Premium", $"Downloaded {fileName} for {mod.Name}.");
        return destination;
    }

    private static HttpRequestMessage CreateRequest(string url, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", apiKey);
        request.Headers.Add("Application-Name", "CasualtiesHub");
        request.Headers.Add("Application-Version", "0.0.8-pre.2");
        return request;
    }

    private static JsonElement SelectLatestFile(JsonElement root)
    {
        if (!root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Nexus returned no files for this mod.");
        var candidates = files.EnumerateArray()
            .Where(file => GetInt(file, "category_id") is 1 or 2)
            .ToList();
        if (candidates.Count == 0) candidates = files.EnumerateArray().ToList();
        return candidates.OrderByDescending(file => GetInt(file, "uploaded_timestamp", "file_id", "id")).First();
    }

    private static int GetInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed)) return parsed;
        return 0;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
        return null;
    }

    private static string MakeSafeFileName(string value) =>
        string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
}
