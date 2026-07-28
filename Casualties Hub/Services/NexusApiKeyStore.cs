using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Casualties_Hub.Services;

/// <summary>Stores a personal Nexus key using Windows' current-user encryption.</summary>
public sealed class NexusApiKeyStore
{
    private readonly string _path;

    public NexusApiKeyStore(SettingsService settingsService) =>
        _path = Path.Combine(settingsService.AppDataPath, "NexusApiKey.dat");

    public bool HasKey => !string.IsNullOrWhiteSpace(Load());

    public void Save(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Clear();
            return;
        }
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey.Trim()), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }

    public string? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
