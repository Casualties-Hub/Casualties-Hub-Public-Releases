using System.Security.Cryptography;
using System.Text;
using Casualties_Hub.Services;
using Xunit;

namespace Casualties_Hub.Tests;

/// <summary>
/// The key store is the one place a credential touches disk. These check that what is written
/// is never the plaintext, that it round-trips, and that the Windows wrapping is actually applied.
/// </summary>
public sealed class NexusApiKeyStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CasualtiesHubTests", Guid.NewGuid().ToString("N"));

    public NexusApiKeyStoreTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void SavedKeyRoundTrips()
    {
        var store = new NexusApiKeyStore(_root);
        store.Save("  abc123-secret  ");

        Assert.True(store.HasKey);
        Assert.Equal("abc123-secret", store.Load());
        Assert.Equal("abc123-secret", new NexusApiKeyStore(_root).Load());
    }

    [Fact]
    public void PlaintextNeverReachesDisk()
    {
        var store = new NexusApiKeyStore(_root);
        store.Save("abc123-secret");

        foreach (var file in Directory.EnumerateFiles(_root))
        {
            var bytes = File.ReadAllBytes(file);
            Assert.Equal(-1, bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes("abc123-secret")));
        }
    }

    [Fact]
    public void ClearRemovesBothFiles()
    {
        var store = new NexusApiKeyStore(_root);
        store.Save("abc123-secret");
        store.Clear();

        Assert.False(store.HasKey);
        Assert.Empty(Directory.EnumerateFiles(_root));
    }

    [Fact]
    public void EncryptionKeyIsWrappedOnWindowsAndBareElsewhere()
    {
        var store = new NexusApiKeyStore(_root);
        store.Save("abc123-secret");

        var keyFile = File.ReadAllBytes(Path.Combine(_root, "NexusApiKey.key"));
        if (OperatingSystem.IsWindows())
            Assert.NotEqual(32, keyFile.Length);
        else
            Assert.Equal(32, keyFile.Length);
    }

    [Fact]
    public void AnUnreadableKeyFileIsReplacedOnTheNextSave()
    {
        // A key file copied from another account or damaged on disk cannot be unwrapped. That
        // must read as "no key" and let a new one be saved, not block every save with an error.
        File.WriteAllBytes(Path.Combine(_root, "NexusApiKey.key"), RandomNumberGenerator.GetBytes(40));
        var store = new NexusApiKeyStore(_root);

        Assert.False(store.HasKey);
        store.Save("fresh-secret");

        Assert.Equal("fresh-secret", new NexusApiKeyStore(_root).Load());
    }

    [Fact]
    public void CorruptedEnvelopeFailsClosed()
    {
        var store = new NexusApiKeyStore(_root);
        store.Save("abc123-secret");

        var dataPath = Path.Combine(_root, "NexusApiKey.dat");
        var bytes = File.ReadAllBytes(dataPath);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(dataPath, bytes);

        Assert.Null(store.Load());
    }

    [Fact]
    public void LegacyWindowsBlobIsReadAndConverted()
    {
        if (!OperatingSystem.IsWindows()) return;

        var dataPath = Path.Combine(_root, "NexusApiKey.dat");
        File.WriteAllBytes(dataPath, ProtectedData.Protect(Encoding.UTF8.GetBytes("legacy-secret"), null, DataProtectionScope.CurrentUser));

        var store = new NexusApiKeyStore(_root);
        Assert.Equal("legacy-secret", store.Load());

        // Converted: the envelope magic now leads the file and a key file exists beside it.
        Assert.StartsWith("CHK1", Encoding.ASCII.GetString(File.ReadAllBytes(dataPath), 0, 4));
        Assert.True(File.Exists(Path.Combine(_root, "NexusApiKey.key")));
    }
}

