using System.IO;

namespace Casualties_Hub.Services;

/// <summary>
/// Reads a data file that ships inside the single-EXE build. A copy on disk beside the Hub
/// always wins, so an unpacked folder install can still hand-edit the catalogs.
/// </summary>
public static class BundledData
{
    /// <summary>Where a file was loaded from, and a stamp callers cache against until it changes.</summary>
    public readonly record struct Payload(string Text, string Source, DateTime StampUtc);

    /// <param name="resourceName">Embedded resource inside the EXE, used when no disk copy exists.</param>
    /// <param name="relativeDiskPaths">Paths under the Hub folder, tried in order before the embedded copy.</param>
    public static Payload? Read(string resourceName, params string[] relativeDiskPaths)
    {
        foreach (var relative in relativeDiskPaths)
        {
            var path = Path.Combine(AppContext.BaseDirectory, relative);
            if (!File.Exists(path)) continue;
            try { return new Payload(File.ReadAllText(path), path, File.GetLastWriteTimeUtc(path)); }
            catch (IOException exception)
            { DebugLogService.Error($"Could not read {relative}; falling back to the bundled copy", exception); }
        }

        using var stream = typeof(BundledData).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        // Embedded content only changes when a new build ships, so the stamp is constant.
        return new Payload(reader.ReadToEnd(), resourceName, DateTime.MinValue);
    }
}
