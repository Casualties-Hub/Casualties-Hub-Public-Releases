using System.IO;
using System.Text.RegularExpressions;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Finds the CustomSprites st# skin slots inside the configured BepInEx\Plugins folder.
/// A slot only counts as present when it actually holds sprite art, so empty leftover folders
/// do not show up as selectable skins.
/// </summary>
public class SkinLibraryService(SettingsService settingsService, ModService modService)
{
    // Installs go through st0-st9, but other sprite mods create higher slots (st10+ are common),
    // so the preview accepts any numeric slot rather than only the ten the install dialog offers.
    private static readonly Regex SlotPattern = new(@"^st(\d+)$", RegexOptions.IgnoreCase);

    public string GetCustomSpritesRoot()
    {
        var pluginsPath = modService.GetPluginsPath(settingsService.Load());
        return string.IsNullOrWhiteSpace(pluginsPath) ? "" : LinuxPaths.ResolveChild(pluginsPath, "CustomSprites");
    }

    public List<SkinSlot> DiscoverSlots()
    {
        var root = GetCustomSpritesRoot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return [];

        return Directory.EnumerateDirectories(root)
            .Select(path => (Path: path, Match: SlotPattern.Match(Path.GetFileName(path))))
            .Where(entry => entry.Match.Success)
            .Select(entry => CreateSlot(entry.Path, int.Parse(entry.Match.Groups[1].Value)))
            .Where(slot => slot.SpriteCount > 0)
            // Numeric order, so st10 does not sort between st1 and st2 the way a string compare would.
            .OrderBy(slot => slot.Number)
            .ToList();
    }

    private static SkinSlot CreateSlot(string path, int number) => new()
    {
        Name = Path.GetFileName(path).ToLowerInvariant(),
        Number = number,
        FolderPath = path,
        HeadSpriteCount = CountPngs(LinuxPaths.ResolveChild(path, "Head")),
        BodySpriteCount = CountPngs(LinuxPaths.ResolveChild(path, "Body")),
        MissingSprites = SkinRig.FindMissingRequiredSprites(path),
    };

    // Case-insensitive: a skin shipping .PNG would otherwise count zero sprites, and DiscoverSlots
    // drops any slot with SpriteCount == 0, so the whole skin would vanish from the picker.
    private static int CountPngs(string folder) =>
        Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.png", LinuxPaths.CaseInsensitive).Count() : 0;
}
