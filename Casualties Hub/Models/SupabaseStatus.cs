namespace Casualties_Hub.Models;

public sealed record SupabaseStatus(
    bool IsOnline,
    string Announcement,
    string? AnnouncementId,
    DateTimeOffset? UpdatedAt,
    bool IsCached,
    DateTimeOffset? NextCheckUtc,
    bool ServerContentChanged = false,
    bool IsMaintenance = false,
    int ActiveUsersLastTwoHours = 0,
    int ActiveUsersLastDay = 0,
    int ActiveUsersLastWeek = 0);
