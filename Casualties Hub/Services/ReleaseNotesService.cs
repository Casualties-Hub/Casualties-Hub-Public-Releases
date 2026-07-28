using System.IO;

namespace Casualties_Hub.Services;

/// <summary>Reads the short, local release summary shown in Hub Center.</summary>
public sealed class ReleaseNotesService
{
    public string GetWhatChanged(string version)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Release Notes", $"Version {version}.txt");
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, $"Version {version}.txt"); // legacy builds
        if (!File.Exists(path))
            return "What changed notes are not available for this build.";

        var lines = File.ReadAllLines(path);
        var summary = new List<string>();
        var readingChanges = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.Equals(line, "What changed", StringComparison.OrdinalIgnoreCase))
            {
                readingChanges = true;
                continue;
            }

            if (!readingChanges)
                continue;

            if (string.Equals(line, "Known issue", StringComparison.OrdinalIgnoreCase))
                break;

            if (line.StartsWith("- ", StringComparison.Ordinal))
                summary.Add($"• {line[2..]}");
        }

        return summary.Count > 0
            ? string.Join(Environment.NewLine, summary)
            : "No feature summary was included for this build.";
    }
}
