namespace Casualties_Hub.Models;

/// <summary>One leftover Temp folder from a past update download or local release install.</summary>
public class UpdateStagingFolder
{
    public required string Path { get; init; }
    public required string Kind { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset LastWriteTimeUtc { get; init; }

    public string DisplaySize => SizeBytes >= 1024 * 1024
        ? $"{SizeBytes / (1024.0 * 1024.0):0.#} MB"
        : $"{Math.Max(SizeBytes / 1024.0, 0.1):0.#} KB";
}
