namespace Casualties_Hub.Models;

/// <summary>
/// A locally saved announcement previously received from the Hub service.
/// The Hub keeps only the newest three entries.
/// </summary>
public sealed class AnnouncementHistoryItem
{
    public string Id { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset ReceivedAtUtc { get; set; }
}
