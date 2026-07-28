namespace Casualties_Hub.Services;

/// <summary>
/// Entries manually designated as adult content for the dashboard. The metadata
/// flag remains supported; this list covers community entries that do not expose it.
/// </summary>
public static class AdultContentCatalog
{
    private static readonly HashSet<string> AdultModNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "randomaddiction",
        "casualtiesconsumed",
        "manualgunsuicide",
        "gouxisfunnyshiiattachmentforgunupdate",
        "memesunknownsoundmod",
        "mushoosscuffedenglishtranslation",
        "casualtiesxl",
        "dglabexpbodysync",
        "razorblade",
        "cigarrets"
    };

    public static bool IsAdult(string? modName)
    {
        var normalized = Normalize(modName);
        return AdultModNames.Contains(normalized)
            || normalized.StartsWith("gouxisfunny", StringComparison.Ordinal);
    }

    private static string Normalize(string? value) => string.Concat((value ?? string.Empty)
        .Where(char.IsLetterOrDigit)
        .Select(char.ToLowerInvariant));
}
