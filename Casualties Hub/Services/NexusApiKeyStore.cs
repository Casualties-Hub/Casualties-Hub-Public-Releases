using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Casualties_Hub.Services;

/// <summary>
/// Stores a personal Nexus API key on disk.
/// </summary>
/// <remarks>
/// <para>
/// The key is sealed with AES-GCM under a random encryption key kept in a file beside it. What
/// protects that encryption key differs by platform. On Windows it is wrapped with DPAPI for the
/// current user, so only this Windows account can unwrap it. Elsewhere it is a 0600 file inside a
/// 0700 directory, so another user cannot read it, but anything running as this user can.
/// </para>
/// <para>
/// The envelope buys three things on every platform: the key never sits in plaintext where a
/// backup, a cloud-sync folder, a screen share or a stray grep would expose it; the AES-GCM
/// authentication tag makes a truncated or corrupted file fail closed rather than send a malformed
/// key to Nexus; and deleting the small .key file instantly renders the stored credential
/// unrecoverable.
/// </para>
/// <para>
/// Earlier Windows builds stored the key as a bare DPAPI blob with no envelope. Such a file is
/// still readable here and is rewritten in the current format the first time it is loaded.
/// </para>
/// </remarks>
public sealed class NexusApiKeyStore
{
    // Identifies our envelope so a file in another format is recognised rather than misread.
    private static readonly byte[] Magic = "CHK1"u8.ToArray();
    private const int NonceSize = 12;   // AES-GCM standard
    private const int TagSize = 16;
    private const int KeySize = 32;     // AES-256

    private readonly string _dataPath;
    private readonly string _keyPath;

    public NexusApiKeyStore(SettingsService settingsService) : this(settingsService.AppDataPath) { }

    /// <summary>Tests point this at a disposable folder instead of the real data directory.</summary>
    internal NexusApiKeyStore(string dataDirectory)
    {
        _dataPath = Path.Combine(dataDirectory, "NexusApiKey.dat");
        _keyPath = Path.Combine(dataDirectory, "NexusApiKey.key");
        HardenDirectory(dataDirectory);
    }

    public bool HasKey => !string.IsNullOrWhiteSpace(Load());

    /// <summary>How the key is protected, shown in Settings so the user is not left guessing.</summary>
    public static string ProtectionDescription => OperatingSystem.IsWindows()
        ? "Encrypted on disk with your Windows account's data protection. Only this account can read it."
        : "Encrypted on disk and readable only by your user account. Anything running as you can still read it.";

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
            CryptographicOperations.ZeroMemory(key);
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
        // failure here must degrade to "no key saved", never take down the page.
        try
        {
            if (!File.Exists(_dataPath)) return null;

            var payload = File.ReadAllBytes(_dataPath);

            if (!payload.AsSpan().StartsWith(Magic))
                return LoadLegacy(payload);

            if (!File.Exists(_keyPath) || payload.Length < Magic.Length + NonceSize + TagSize) return null;

            var key = LoadKey();
            if (key is null) return null;

            var offset = Magic.Length;
            var nonce = payload.AsSpan(offset, NonceSize);
            var tag = payload.AsSpan(offset + NonceSize, TagSize);
            var ciphertext = payload.AsSpan(offset + NonceSize + TagSize);
            var plaintext = new byte[ciphertext.Length];

            using (var aes = new AesGcm(key, TagSize))
                aes.Decrypt(nonce, ciphertext, tag, plaintext);

            var result = Encoding.UTF8.GetString(plaintext);
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);
            return result;
        }
        catch (CryptographicException)
        {
            // Authentication failed: the file is corrupt, the key no longer matches it, or DPAPI
            // could not unwrap the key for this account.
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

    /// <summary>
    /// A .dat with no envelope is the bare DPAPI blob earlier Windows builds wrote. It can only be
    /// unwrapped on Windows by the account that wrote it; on success it is re-saved in the current
    /// format so the next load takes the normal path.
    /// </summary>
    private string? LoadLegacy(byte[] payload)
    {
        if (!OperatingSystem.IsWindows())
        {
            DebugLogService.Info("The saved Nexus key is not in this platform's format; re-enter it in Settings.");
            return null;
        }

        var apiKey = Encoding.UTF8.GetString(ProtectedData.Unprotect(payload, null, DataProtectionScope.CurrentUser));
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        // The rewrite is a courtesy, not a requirement: the legacy blob still reads fine, so a
        // folder that cannot be written to right now must not turn "key present" into a crash.
        try
        {
            Save(apiKey);
            DebugLogService.Activity("Nexus key", "Converted a key saved by an earlier build to the current format.");
        }
        catch (InvalidOperationException exception)
        {
            DebugLogService.Error("Kept the earlier key format; it could not be rewritten", exception);
        }

        return apiKey;
    }

    private byte[] LoadOrCreateKey()
    {
        if (LoadKey() is { } existing) return existing;

        var key = RandomNumberGenerator.GetBytes(KeySize);
        WriteRestricted(_keyPath, Wrap(key));
        return key;
    }

    /// <summary>
    /// The encryption key, or null when the file is missing or not usable by this account. Null
    /// makes the next save start a fresh key; the old payload then fails to decrypt and the user
    /// is asked to re-enter it, which is the right outcome for a key file that cannot be read.
    /// </summary>
    private byte[]? LoadKey()
    {
        if (!File.Exists(_keyPath)) return null;
        var stored = File.ReadAllBytes(_keyPath);
        if (!OperatingSystem.IsWindows()) return stored.Length == KeySize ? stored : null;

        // A bare key from before DPAPI wrapping is accepted; it is wrapped the next time a key
        // is saved, because that is the only time the file is rewritten.
        if (stored.Length == KeySize) return stored;

        try
        {
            var unwrapped = ProtectedData.Unprotect(stored, null, DataProtectionScope.CurrentUser);
            return unwrapped.Length == KeySize ? unwrapped : null;
        }
        catch (CryptographicException)
        {
            // Written by another Windows account or machine, or damaged. Same outcome as a
            // missing file rather than an error the user cannot act on.
            DebugLogService.Info("The Nexus key file was not written by this Windows account; a new one will be created on the next save.");
            return null;
        }
    }

    /// <summary>On Windows the key file is bound to the current account through DPAPI.</summary>
    private static byte[] Wrap(byte[] key) => OperatingSystem.IsWindows()
        ? ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser)
        : key;

    /// <summary>Writes a file only this user can read, without ever letting it exist as 0644.</summary>
    private static void WriteRestricted(string path, byte[] contents)
    {
        // Create first, tighten the mode, then write: setting permissions afterwards would leave
        // a window where the key is world-readable. Windows relies on the profile folder's ACL.
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
