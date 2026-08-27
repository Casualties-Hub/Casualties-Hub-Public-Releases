using System.IO;

namespace Casualties_Hub.Services;

/// <summary>
/// Path handling that works on a case-sensitive filesystem.
///
/// A BepInEx install may create either <c>BepInEx/Plugins</c> or <c>BepInEx/plugins</c>. On ext4
/// only one of those matches a hardcoded spelling, so fixed-casing lookups report "no plugins
/// folder" while pointing at a perfectly good game directory.
///
/// The rule used throughout:
///   DISCOVERY (finding something that exists)  -> case-insensitive
///   IDENTITY  (is this the same file?)         -> ordinal, always
///
/// Case-insensitive identity is not merely unnecessary, it is unsafe: on a case-sensitive
/// filesystem two genuinely different files compare equal, which matters where paths guard
/// deletion or containment.
/// </summary>
public static class LinuxPaths
{
    /// <summary>Matches files regardless of extension casing, so a mod shipping <c>Foo.DLL</c> is still seen.</summary>
    public static EnumerationOptions CaseInsensitive { get; } = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        IgnoreInaccessible = true,
    };

    public static EnumerationOptions CaseInsensitiveRecursive { get; } = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
    };

    /// <summary>
    /// The real on-disk path of a child entry, matched without regard to case. Returns null when
    /// nothing matches. Use this to FIND things.
    /// </summary>
    public static string? FindChild(string parent, string name)
    {
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) return null;
        try
        {
            var exact = Path.Combine(parent, name);
            if (Directory.Exists(exact) || File.Exists(exact)) return exact;

            return Directory.EnumerateFileSystemEntries(parent)
                .FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), name, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The real path of a child if it exists, otherwise the exact-cased path so callers can create
    /// it. Use this when a path may need to be built as well as found.
    /// </summary>
    public static string ResolveChild(string parent, string name) =>
        FindChild(parent, name) ?? Path.Combine(parent, name);

    /// <summary>Walks several levels, matching each without regard to case.</summary>
    public static string ResolveChain(string root, params string[] names)
    {
        var current = root;
        foreach (var name in names) current = ResolveChild(current, name);
        return current;
    }

    /// <summary>
    /// True when <paramref name="candidate"/> sits inside <paramref name="container"/>.
    /// Ordinal by design: this backs containment checks, where a case-insensitive comparison on a
    /// case-sensitive filesystem would let two different directories test as the same one.
    /// </summary>
    public static bool IsInside(string candidate, string container)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(container)) return false;

        var normalizedContainer = Path.GetFullPath(container).TrimEnd(Path.DirectorySeparatorChar);
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar);

        return normalizedCandidate.Equals(normalizedContainer, StringComparison.Ordinal)
            || normalizedCandidate.StartsWith(normalizedContainer + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Hub's data directory, guaranteed absolute.
    /// </summary>
    /// <remarks>
    /// <c>GetFolderPath(LocalApplicationData)</c> defaults to <c>SpecialFolderOption.None</c>, which
    /// on Unix returns an EMPTY STRING when the directory does not exist yet rather than creating
    /// it. <c>Path.Combine("", "CasualtiesHub")</c> then yields the RELATIVE path "CasualtiesHub",
    /// and the Hub would write settings, logs and the Nexus API key into whatever the working
    /// directory happened to be. Fresh accounts, minimal installs and containers all hit this.
    /// </remarks>
    public static string AppDataRoot()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            // Last resort if even Create failed: follow the XDG spec by hand.
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            localAppData = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(localAppData, "CasualtiesHub");
    }

    /// <summary>
    /// The user's downloads folder, honouring XDG so localised desktops resolve correctly
    /// (~/Téléchargements, ~/下载) instead of a hardcoded ~/Downloads that does not exist.
    /// </summary>
    public static string DownloadsFolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userDirs = Path.Combine(home, ".config", "user-dirs.dirs");

        if (File.Exists(userDirs))
        {
            try
            {
                foreach (var line in File.ReadAllLines(userDirs))
                {
                    var trimmed = line.TrimStart();
                    if (!trimmed.StartsWith("XDG_DOWNLOAD_DIR=", StringComparison.Ordinal)) continue;

                    var value = trimmed["XDG_DOWNLOAD_DIR=".Length..].Trim().Trim('"');
                    value = value.Replace("$HOME", home, StringComparison.Ordinal);
                    if (value.Length > 0 && Directory.Exists(value)) return value;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Fall through to the default below.
            }
        }

        return Path.Combine(home, "Downloads");
    }
}
