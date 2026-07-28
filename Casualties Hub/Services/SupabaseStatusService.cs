using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Casualties_Hub.Models;

namespace Casualties_Hub.Services;

/// <summary>
/// Retrieves one public announcement through a rate-limited Supabase RPC route.
/// The desktop client owns only a publishable key; database writes remain impossible.
/// </summary>
public sealed class SupabaseStatusService
{
    private const string ProjectUrl = "https://sotnptvbzwnlukcmjweh.supabase.co";
    private const string PublishableKey = "sb_publishable_TlCpjP8q8u2H52Ucw784MA_2Wfo_2bm";
    // Automatic polling stays at 30 minutes. The server separately permits
    // an installation/IP to request status no more often than every 7.5 minutes.
    private static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ManualCheckCooldown = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RequestWindow = TimeSpan.FromHours(1);
    private const int MaximumRequestsPerWindow = 8;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly SettingsService _settingsService = new();

    public SupabaseStatus LoadCached()
    {
        var settings = _settingsService.Load();
        if (!settings.HubOnlineServicesEnabled)
        {
            DebugLogService.Activity("Supabase", "Hub Online Services are disabled in Settings; no server request was made.");
            return BuildCachedStatus(settings);
        }
        return BuildCachedStatus(settings);
    }

    public bool CanMakeManualCheck(out DateTimeOffset? availableAtUtc)
    {
        var settings = _settingsService.Load();
        var now = DateTimeOffset.UtcNow;
        settings.SupabaseRequestHistoryUtc ??= [];
        settings.SupabaseRequestHistoryUtc.RemoveAll(requestedAt => now - requestedAt >= RequestWindow);

        if (settings.NextManualSupabaseCheckUtc is { } manualNext && manualNext > now)
        {
            availableAtUtc = manualNext;
            return false;
        }

        if (settings.SupabaseRequestHistoryUtc.Count >= MaximumRequestsPerWindow)
        {
            availableAtUtc = settings.SupabaseRequestHistoryUtc.Min().Add(RequestWindow);
            return false;
        }

        availableAtUtc = null;
        return true;
    }

    public Task<SupabaseStatus> GetManualStatusAsync(CancellationToken cancellationToken = default) =>
        GetStatusAsync(forceNetworkCheck: true, isManualCheck: true, cancellationToken: cancellationToken);

    public async Task<SupabaseStatus> GetStatusAsync(bool forceNetworkCheck = false, bool isManualCheck = false, CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Load();
        var now = DateTimeOffset.UtcNow;
        if (isManualCheck && !CanMakeManualCheck(out var manualAvailableAt))
        {
            settings.NextManualSupabaseCheckUtc = manualAvailableAt;
            _settingsService.Save(settings);
            DebugLogService.Activity("Supabase", "Manual check used cached data because its 15-minute cooldown or the eight-per-hour cap is active.");
            return BuildCachedStatus(settings);
        }
        if (!forceNetworkCheck && settings.NextSupabaseCheckUtc is { } nextCheck && nextCheck > now)
        {
            DebugLogService.Activity("Supabase", $"Used the cached announcement; next network check is allowed at {nextCheck:O}.");
            return BuildCachedStatus(settings);
        }

        if (!TryRegisterOutgoingRequest(settings, now, out var nextAllowedRequest))
        {
            settings.NextSupabaseCheckUtc = nextAllowedRequest;
            _settingsService.Save(settings);
            DebugLogService.Activity("Supabase", "Local eight-requests-per-hour guard used cached server data.");
            return BuildCachedStatus(settings);
        }

        if (isManualCheck)
            settings.NextManualSupabaseCheckUtc = now + ManualCheckCooldown;

        var installationId = GetOrCreateInstallationId(settings);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProjectUrl}/rest/v1/rpc/get_hub_status")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { p_installation_id = installationId }), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("apikey", PublishableKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PublishableKey);
            using var response = await _client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                settings.NextSupabaseCheckUtc = now + (response.StatusCode == (HttpStatusCode)429 ? TimeSpan.FromHours(1) : MinimumCheckInterval);
                _settingsService.Save(settings);
                DebugLogService.Activity("Supabase", response.StatusCode == (HttpStatusCode)429
                    ? "Supabase rate limit reached; cached announcement will be used for one hour."
                    : $"Supabase status request returned {(int)response.StatusCode}; cached announcement will be used.");
                return BuildCachedStatus(settings);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                settings.NextSupabaseCheckUtc = now + MinimumCheckInterval;
                _settingsService.Save(settings);
                return BuildCachedStatus(settings);
            }

            var row = document.RootElement[0];
            var announcement = row.TryGetProperty("announcement", out var announcementValue)
                ? announcementValue.GetString()?.Trim() ?? "" : "";
            var announcementId = row.TryGetProperty("announcement_id", out var idValue) ? idValue.GetString() : null;
            var compatibilityRules = row.TryGetProperty("compatibility_rules", out var rulesValue) ? rulesValue.GetString() : null;
            var compatibilityVersion = row.TryGetProperty("compatibility_version", out var compatibilityVersionValue) ? compatibilityVersionValue.GetString() : null;
            var activeUsersLastTwoHours = ReadCount(row, "active_users_last_two_hours");
            var activeUsersLastDay = ReadCount(row, "active_users_last_day");
            var activeUsersLastWeek = ReadCount(row, "active_users_last_week");
            var parsedUpdatedAt = default(DateTimeOffset);
            if (row.TryGetProperty("updated_at", out var updatedValue))
                DateTimeOffset.TryParse(updatedValue.GetString(), out parsedUpdatedAt);
            DateTimeOffset? updatedAt = parsedUpdatedAt == default ? null : parsedUpdatedAt;

            var hadCachedServerData = settings.LastSupabaseCheckUtc is not null;
            var normalizedAnnouncement = string.IsNullOrWhiteSpace(announcement) ? "No announcement right now." : announcement;
            var announcementChanged = !string.Equals(settings.CachedAnnouncement, normalizedAnnouncement, StringComparison.Ordinal)
                || !string.Equals(settings.CachedAnnouncementId, announcementId, StringComparison.Ordinal);
            var compatibilityChanged = !string.IsNullOrWhiteSpace(compatibilityRules)
                && (!string.Equals(settings.CachedCompatibilityRules, compatibilityRules, StringComparison.Ordinal)
                    || !string.Equals(settings.CachedCompatibilityVersion, compatibilityVersion, StringComparison.Ordinal));

            settings.CachedAnnouncement = normalizedAnnouncement;
            settings.CachedAnnouncementId = announcementId;
            settings.CachedAnnouncementUpdatedAt = updatedAt;
            settings.CachedActiveUsersLastTwoHours = activeUsersLastTwoHours;
            settings.CachedActiveUsersLastDay = activeUsersLastDay;
            settings.CachedActiveUsersLastWeek = activeUsersLastWeek;
            SaveAnnouncementToHistory(settings, normalizedAnnouncement, announcementId, updatedAt ?? now);
            if (!string.IsNullOrWhiteSpace(compatibilityRules) && CompatibilityFeedService.TryParse(compatibilityRules, out _))
            {
                settings.CachedCompatibilityRules = compatibilityRules;
                settings.CachedCompatibilityVersion = compatibilityVersion;
                settings.CachedCompatibilityUpdatedAt = updatedAt;
            }
            settings.LastSupabaseCheckUtc = now;
            settings.NextSupabaseCheckUtc = now + MinimumCheckInterval;
            _settingsService.Save(settings);
            var serverContentChanged = hadCachedServerData && (announcementChanged || compatibilityChanged);
            DebugLogService.Activity("Supabase", serverContentChanged
                ? "Received changed server data; the active page will refresh from the updated local cache."
                : "Loaded live server data through the rate-limited RPC and replaced the local cache.");
            return new SupabaseStatus(
                true,
                settings.CachedAnnouncement,
                announcementId,
                updatedAt,
                false,
                settings.NextSupabaseCheckUtc,
                serverContentChanged,
                IsMaintenanceAnnouncement(announcementId),
                activeUsersLastTwoHours,
                activeUsersLastDay,
                activeUsersLastWeek);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            settings.NextSupabaseCheckUtc = now + MinimumCheckInterval;
            _settingsService.Save(settings);
            DebugLogService.Activity("Supabase", "The Hub update service could not be reached; using the cached announcement.");
            return BuildCachedStatus(settings);
        }
    }

    public SupabaseStatus CreateRateLimitTestStatus()
    {
        var settings = _settingsService.Load();
        settings.NextSupabaseCheckUtc = DateTimeOffset.UtcNow.AddHours(1);
        _settingsService.Save(settings);
        return BuildCachedStatus(settings);
    }

    private static SupabaseStatus BuildCachedStatus(Settings settings)
    {
        var recentlyConnected = settings.LastSupabaseCheckUtc is { } lastCheck && DateTimeOffset.UtcNow - lastCheck <= MinimumCheckInterval;
        return new SupabaseStatus(
            recentlyConnected,
            settings.CachedAnnouncement ?? "Announcements are unavailable until the Hub connects to its update service.",
            settings.CachedAnnouncementId,
            settings.CachedAnnouncementUpdatedAt,
            true,
            settings.NextSupabaseCheckUtc,
            false,
            IsMaintenanceAnnouncement(settings.CachedAnnouncementId),
            settings.CachedActiveUsersLastTwoHours,
            settings.CachedActiveUsersLastDay,
            settings.CachedActiveUsersLastWeek);
    }

    // No database migration is needed: use an announcement ID such as
    // "maintenance-2026-07-26" when the Hub service is intentionally down.
    private static bool IsMaintenanceAnnouncement(string? announcementId) =>
        !string.IsNullOrWhiteSpace(announcementId)
        && announcementId.StartsWith("maintenance-", StringComparison.OrdinalIgnoreCase);

    private static int ReadCount(JsonElement row, string propertyName) =>
        row.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var count)
            ? Math.Max(0, count)
            : 0;

    private static string GetOrCreateInstallationId(Settings settings)
    {
        if (!Guid.TryParse(settings.InstallationId, out var installationId))
        {
            installationId = Guid.NewGuid();
            settings.InstallationId = installationId.ToString("D");
            DebugLogService.Activity("Supabase", "Created an anonymous installation ID for aggregate activity counts.");
        }

        return installationId.ToString("D");
    }

    private static void SaveAnnouncementToHistory(Settings settings, string announcement, string? announcementId, DateTimeOffset receivedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(announcement) || announcement == "No announcement right now.")
            return;

        settings.AnnouncementHistory ??= [];
        var id = string.IsNullOrWhiteSpace(announcementId) ? announcement : announcementId;
        settings.AnnouncementHistory.RemoveAll(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        settings.AnnouncementHistory.Insert(0, new AnnouncementHistoryItem
        {
            Id = id,
            Message = announcement,
            ReceivedAtUtc = receivedAtUtc
        });

        if (settings.AnnouncementHistory.Count > 3)
            settings.AnnouncementHistory.RemoveRange(3, settings.AnnouncementHistory.Count - 3);
    }

    private static bool TryRegisterOutgoingRequest(Settings settings, DateTimeOffset now, out DateTimeOffset nextAllowedRequest)
    {
        settings.SupabaseRequestHistoryUtc ??= [];
        settings.SupabaseRequestHistoryUtc.RemoveAll(requestedAt => now - requestedAt >= RequestWindow);

        if (settings.SupabaseRequestHistoryUtc.Count >= MaximumRequestsPerWindow)
        {
            nextAllowedRequest = settings.SupabaseRequestHistoryUtc.Min().Add(RequestWindow);
            return false;
        }

        settings.SupabaseRequestHistoryUtc.Add(now);
        nextAllowedRequest = now;
        return true;
    }
}
