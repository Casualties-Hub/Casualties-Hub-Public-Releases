using System.Globalization;
using System.Text.RegularExpressions;

namespace Casualties_Hub.Models;

/// <summary>A small SemVer reader used for the Hub's stable and prerelease channels.</summary>
public sealed class HubVersion : IComparable<HubVersion>
{
    private static readonly Regex Pattern = new(@"^v?(?<version>\d+\.\d+\.\d+)(?:-(?<pre>[0-9A-Za-z.-]+))?$", RegexOptions.Compiled);

    public HubVersion(Version version, string? prerelease, string original)
    {
        Version = version;
        Prerelease = string.IsNullOrWhiteSpace(prerelease) ? null : prerelease;
        Original = original;
    }

    public Version Version { get; }
    public string? Prerelease { get; }
    public string Original { get; }
    public bool IsPrerelease => Prerelease is not null;

    public static HubVersion Current()
    {
        var informational = typeof(HubVersion).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        return TryParse(informational, out var parsed)
            ? parsed
            : new HubVersion(typeof(HubVersion).Assembly.GetName().Version ?? new Version(0, 0, 0), null, "0.0.0");
    }

    public static bool TryParse(string? value, out HubVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var clean = value.Split('+')[0].Trim();
        var match = Pattern.Match(clean);
        if (!match.Success || !Version.TryParse(match.Groups["version"].Value, out var numeric)) return false;
        version = new HubVersion(numeric, match.Groups["pre"].Value, clean.TrimStart('v', 'V'));
        return true;
    }

    public int CompareTo(HubVersion? other)
    {
        if (other is null) return 1;
        var numeric = Version.CompareTo(other.Version);
        if (numeric != 0) return numeric;
        if (IsPrerelease && !other.IsPrerelease) return -1;
        if (!IsPrerelease && other.IsPrerelease) return 1;
        if (!IsPrerelease) return 0;
        return ComparePrerelease(Prerelease!, other.Prerelease!);
    }

    private static int ComparePrerelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        for (var index = 0; index < Math.Max(leftParts.Length, rightParts.Length); index++)
        {
            if (index == leftParts.Length) return -1;
            if (index == rightParts.Length) return 1;
            var a = leftParts[index];
            var b = rightParts[index];
            var aNumber = int.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var aNumeric);
            var bNumber = int.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var bNumeric);
            var result = aNumber && bNumber ? aNumeric.CompareTo(bNumeric)
                : aNumber ? -1 : bNumber ? 1 : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            if (result != 0) return result;
        }
        return 0;
    }

    public override string ToString() => Original;
}
