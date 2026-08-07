namespace Casualties_Hub.Models;

/// <summary>
/// An announcement this PC has already received. History is kept locally so a
/// player keeps their own record even after the published feed stops listing an
/// older announcement. The Hub keeps only the newest three.
/// </summary>
public sealed class AnnouncementHistoryItem
{
    public string Id { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset PublishedAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
}
