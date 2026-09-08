using System.IO;

namespace Casualties_Hub.Services;

/// <summary>
/// Reads What Changed and release information from the build's own bundled
/// Release Notes file. This is local to the installed version — it only
/// changes when a new build ships, never from the GitHub-hosted feed.
/// </summary>
public sealed class ReleaseNotesService
{
    private static readonly string[] SectionHeadings = ["What changed", "Release information", "Known issue", "Testing note"];

    public string GetWhatChanged(string version)
    {
        var lines = ReadReleaseNotes(version);
        if (lines is null) return "What changed notes are not available for this build.";

        var summary = new List<string>();
        foreach (var line in ReadSection(lines, "What changed"))
            if (line.StartsWith("- ", StringComparison.Ordinal))
                summary.Add($"• {line[2..]}");

        return summary.Count > 0
            ? string.Join(Environment.NewLine, summary)
            : "No feature summary was included for this build.";
    }

    public string GetReleaseInformation(string version)
    {
        var lines = ReadReleaseNotes(version);
        if (lines is null) return "No additional release information is available.";

        // "Release information" is the canonical heading; "Testing note" is the
        // wording used on pre-release build notes and means the same thing.
        var section = ReadSection(lines, "Release information").ToList();
        if (section.Count == 0) section = ReadSection(lines, "Testing note").ToList();

        return section.Count > 0
            ? string.Join(" ", section)
            : "No additional release information is available.";
    }

    private static string[]? ReadReleaseNotes(string version)
    {
        var loaded = BundledData.Read(
            $"Bundled/Release Notes/Version {version}.txt",
            Path.Combine("Data", "Release Notes", $"Version {version}.txt"),
            $"Version {version}.txt"); // legacy builds
        return loaded?.Text.ReplaceLineEndings("\n").Split('\n');
    }

    /// <summary>Lines under the given heading, stopping at the next recognised heading or the end of the file.</summary>
    private static IEnumerable<string> ReadSection(string[] lines, string heading)
    {
        var reading = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (!reading)
            {
                if (string.Equals(line, heading, StringComparison.OrdinalIgnoreCase)) reading = true;
                continue;
            }

            if (line.Length == 0) continue;
            if (SectionHeadings.Any(other => string.Equals(line, other, StringComparison.OrdinalIgnoreCase))) yield break;

            yield return line;
        }
    }
}
