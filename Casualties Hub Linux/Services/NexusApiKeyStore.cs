using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Casualties_Hub.Services;

/// <summary>
/// Stores a personal Nexus API key on disk.
/// </summary>
/// <remarks>
/// <para>
/// The Windows Hub uses DPAPI (<c>ProtectedData</c>), which has no Unix implementation and throws
/// <see cref="PlatformNotSupportedException"/> the moment it is touched. Because HasKey calls Load,
/// that exception would surface just from opening the Settings page.
/// </para>
/// <para>
/// <b>What actually protects the key here is file permissions, not the encryption.</b> The key file
/// is 0600 inside a 0700 directory, so another user cannot read it, but anything running as this
/// user can. Encrypting with a key stored beside the ciphertext does not change that, and it would
/// be dishonest to describe it as though it did.
/// </para>
/// <para>
/// The encryption still earns its place for three narrower reasons: the key never sits in
/// plaintext where a backup, a cloud-sync folder, a screen share or a stray grep would expose it;
/// the AES-GCM authentication tag makes a truncated or corrupted file fail closed rather than send
/// a malformed key to Nexus; and deleting the small .key file instantly renders the stored
/// credential unrecoverable, which is a usable panic button.
/// </para>
/// </remarks>
public sealed class NexusApiKeyStore
{
    // Identifies our envelope so a file from another platform is rejected rather than misread.
    private static readonly byte[] Magic = "CHK1"u8.ToArray();
    private const int NonceSize = 12;   // AES-GCM standard
    private const int TagSize = 16;
    private const int KeySize = 32;     // AES-256

    private readonly string _dataPath;
    private readonly string _keyPath;

    public NexusApiKeyStore(SettingsService settingsService)
    {
        _dataPath = Path.Combine(settingsService.AppDataPath, "NexusApiKey.dat");
        _keyPath = Path.Combine(settingsService.AppDataPath, "NexusApiKey.key");
        HardenDirectory(Path.GetDirectoryName(_dataPath)!);
    }

    public bool HasKey => !string.IsNullOrWhiteSpace(Load());

    /// <summary>How the key is protected, shown in Settings so the user is not left guessing.</summary>
    public static string ProtectionDescription =>
        "Encrypted on disk and readable only by your user account. Anything running as you can still read it.";

    public void Save(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Clear();
            return;
        }

        try
        {
            var key = LoadOrCreateKey();
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plaintext = Encoding.UTF8.GetBytes(apiKey.Trim());
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(key, TagSize))
                aes.Encrypt(nonce, plaintext, ciphertext, tag);

            // [magic][nonce][tag][ciphertext]
            using var stream = new MemoryStream();
            stream.Write(Magic);
            stream.Write(nonce);
            stream.Write(tag);
            stream.Write(ciphertext);

            WriteRestricted(_dataPath, stream.ToArray());
            CryptographicOperations.ZeroMemory(plaintext);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            DebugLogService.Error("Could not save the Nexus API key", exception);
            throw new InvalidOperationException("The Nexus API key could not be saved. Check permissions on your data folder.", exception);
        }
    }

    public string? Load()
    {
        // Deliberately broad: this runs from HasKey, which the Settings page hits on load. A
        // failure here must degrade to "no key saved", never take down the page. The Windows
        // version caught only CryptographicException, which is what made it fatal on Linux.
        try
        {
            if (!File.Exists(_dataPath) || !File.Exists(_keyPath)) return null;

            var payload = File.ReadAllBytes(_dataPath);
            if (payload.Length < Magic.Length + NonceSize + TagSize) return null;

            // A .dat copied from a Windows install is DPAPI-encrypted and unreadable here.
            // Fail closed with a clear message instead of returning garbage to the Nexus API.
            if (!payload.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            {
                DebugLogService.Info("The saved Nexus key is not in this platform's format; re-enter it in Settings.");
                return null;
            }

            var key = File.ReadAllBytes(_keyPath);
            if (key.Length != KeySize) return null;

            var offset = Magic.Length;
            var nonce = payload.AsSpan(offset, NonceSize);
            var tag = payload.AsSpan(offset + NonceSize, TagSize);
            var ciphertext = payload.AsSpan(offset + NonceSize + TagSize);
            var plaintext = new byte[ciphertext.Length];

            using (var aes = new AesGcm(key, TagSize))
                aes.Decrypt(nonce, ciphertext, tag, plaintext);

            var result = Encoding.UTF8.GetString(plaintext);
            CryptographicOperations.ZeroMemory(plaintext);
            return result;
        }
        catch (CryptographicException)
        {
            // Authentication failed: the file is corrupt or the key no longer matches it.
            DebugLogService.Info("The saved Nexus key could not be decrypted; re-enter it in Settings.");
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            DebugLogService.Error("Could not read the saved Nexus API key", exception);
            return null;
        }
    }

    public void Clear()
    {
        foreach (var path in new[] { _dataPath, _keyPath })
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                DebugLogService.Error($"Could not delete {Path.GetFileName(path)}", exception);
            }
        }
    }

    /// <summary>Files the uninstaller must remove. Both, or a stale key file is left behind.</summary>
    public IReadOnlyList<string> StoredFiles => [_dataPath, _keyPath];

    private byte[] LoadOrCreateKey()
    {
        if (File.Exists(_keyPath))
        {
            var existing = File.ReadAllBytes(_keyPath);
            if (existing.Length == KeySize) return existing;
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        WriteRestricted(_keyPath, key);
        return key;
    }

    /// <summary>Writes a file only this user can read, without ever letting it exist as 0644.</summary>
    private static void WriteRestricted(string path, byte[] contents)
    {
        // Create first, tighten the mode, then write: setting permissions afterwards would leave
        // a window where the key is world-readable.
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            stream.Write(contents);
        }
    }

    private static void HardenDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DebugLogService.Error("Could not restrict permissions on the Hub data folder", exception);
        }
    }
}
