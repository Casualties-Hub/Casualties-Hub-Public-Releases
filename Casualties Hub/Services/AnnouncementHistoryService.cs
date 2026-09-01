using System.IO;
using System.Text.Json;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Saves announcements to this PC as they arrive so Hub Home can show recent
/// history without the published configuration having to carry it. Nothing is
/// uploaded; the file only ever contains announcements GitHub served publicly.
/// </summary>
public sealed class AnnouncementHistoryService
{
    /// <summary>How many past announcements Hub Home displays.</summary>
    public const int DisplayedHistoryCount = 3;

    // One extra slot is stored so the announcement that is current today is
    // still on record once it is replaced and becomes history.
    private const int StoredCount = DisplayedHistoryCount + 1;

    private static readonly object HistorySync = new();
    private readonly string _historyPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public AnnouncementHistoryService(SettingsService settingsService)
        => _historyPath = Path.Combine(settingsService.AppDataPath, "AnnouncementHistory.json");

    public IReadOnlyList<AnnouncementHistoryItem> Load()
    {
        lock (HistorySync) return LoadUnsafe();
    }

    /// <summary>
    /// Records everything the configuration currently offers, then returns the
    /// past announcements to display. The announcement that is current right now
    /// is stored but withheld from history, because Hub Home shows it separately.
    /// </summary>
    public IReadOnlyList<AnnouncementHistoryItem> Record(HubConfig config)
    {
        lock (HistorySync)
        {
            var known = new Dictionary<string, AnnouncementHistoryItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in LoadUnsafe()) known[item.Id] = item;

            var receivedAtUtc = DateTimeOffset.UtcNow;
            var added = false;
            foreach (var announcement in Publishable(config))
            {
                if (known.ContainsKey(announcement.Id)) continue;
                known[announcement.Id] = new AnnouncementHistoryItem
                {
                    Id = announcement.Id,
                    Message = announcement.Message,
                    PublishedAtUtc = announcement.PublishedAtUtc,
                    ReceivedAtUtc = receivedAtUtc
                };
                added = true;
            }

            var stored = known.Values
                .OrderByDescending(item => item.PublishedAtUtc)
                .Take(StoredCount)
                .ToList();
            // Hub Home re-reads this on every refresh, so only touch the disk
            // when an announcement was genuinely new.
            if (added) SaveUnsafe(stored);

            return stored
                .Where(item => !string.Equals(item.Id, config.CurrentAnnouncement?.Id, StringComparison.OrdinalIgnoreCase))
                .Take(DisplayedHistoryCount)
                .ToList();
        }
    }

    /// <summary>Announcements worth keeping, newest first.</summary>
    private static IEnumerable<HubAnnouncement> Publishable(HubConfig config)
    {
        var announcements = config.CurrentAnnouncement is { } current
            ? config.PreviousAnnouncements.Prepend(current)
            : config.PreviousAnnouncements;

        return announcements.Where(announcement => !string.IsNullOrWhiteSpace(announcement.Message)
            && !string.IsNullOrWhiteSpace(announcement.Id));
    }

    private List<AnnouncementHistoryItem> LoadUnsafe()
    {
        try
        {
            if (!File.Exists(_historyPath)) return [];
            return JsonSerializer.Deserialize<List<AnnouncementHistoryItem>>(File.ReadAllText(_historyPath), _jsonOptions) ?? [];
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged history file must never stop the Hub from loading; the
            // next announcement simply starts the record again.
            DebugLogService.Error("Could not load AnnouncementHistory.json", exception);
            return [];
        }
    }

    private void SaveUnsafe(List<AnnouncementHistoryItem> history)
    {
        try
        {
            var temporaryPath = _historyPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(history, _jsonOptions));
            File.Move(temporaryPath, _historyPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DebugLogService.Error("Could not save AnnouncementHistory.json", exception);
        }
    }
}
