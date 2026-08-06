using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Casualties_Hub.Services;

/// <summary>Stores the player's Nexus OAuth tokens using Windows' current-user encryption.</summary>
public sealed class NexusOAuthTokenStore
{
    private readonly string _path;

    public NexusOAuthTokenStore(SettingsService settingsService) =>
        _path = Path.Combine(settingsService.AppDataPath, "NexusOAuthTokens.dat");

    public void Save(NexusTokenSet tokens)
    {
        var json = JsonSerializer.Serialize(tokens);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }

    public NexusTokenSet? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser));
            return JsonSerializer.Deserialize<NexusTokenSet>(json);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}

public sealed class NexusTokenSet
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public string? Username { get; init; }
    public bool IsPremium { get; init; }
}
