using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Announcements fetched from the project's GitHub content feed, plus the release notes embedded
/// in this build.
/// </summary>
/// <remarks>
/// The page reads its services directly, so the shell does not have to know what Hub Home displays.
/// Remote content is treated as untrusted: it only ever becomes TextBlock text, and links go
/// through LinuxShell, which allow-lists the scheme.
/// </remarks>
public partial class HubHomePage : UserControl
{
    private const string ReleasesUrl = "https://github.com/Casualties-Hub/Casualties-Hub-Public-Releases/releases";
    private const string NexusUrl = "https://www.nexusmods.com/casualtiesunknown";
    private const string DiscordUrl = "https://discord.gg/casualties";

    private readonly SettingsService _settingsService = new();
    private readonly GitHubHubContentService _contentService;
    private readonly AnnouncementHistoryService _historyService;
    private readonly ReleaseNotesService _releaseNotes = new();
    private readonly Action<string> _setStatus;
    private readonly Action? _openCredits;

    public HubHomePage() : this(_ => { }) { }

    public HubHomePage(Action<string> setStatus, Action? openCredits = null)
    {
        _setStatus = setStatus;
        _openCredits = openCredits;
        _contentService = new GitHubHubContentService(_settingsService);
        _historyService = new AnnouncementHistoryService(_settingsService);
        AvaloniaXamlLoader.Load(this);

        this.FindControl<Button>("CheckButton")!.Click += async (_, _) => await RefreshAsync(force: true);
        this.FindControl<Button>("HistoryButton")!.Click += (_, _) => Toggle("HistoryCard");
        this.FindControl<Button>("ReleaseInfoButton")!.Click += (_, _) => Toggle("ReleaseInfoText");
        this.FindControl<Button>("ReleasesButton")!.Click += (_, _) => LinuxShell.OpenUrl(ReleasesUrl);
        this.FindControl<Button>("NexusButton")!.Click += (_, _) => LinuxShell.OpenUrl(NexusUrl);
        this.FindControl<Button>("DiscordButton")!.Click += (_, _) => LinuxShell.OpenUrl(DiscordUrl);
        this.FindControl<Button>("CreditsButton")!.Click += (_, _) => _openCredits?.Invoke();

        ShowLocalNotes();
        ShowContent(_contentService.LoadCached());

        // Only reach out if the cached copy is stale, so opening this page is not a network hit.
        if (_contentService.IsCheckDue()) _ = RefreshAsync(force: false);
    }

    private void Toggle(string controlName)
    {
        var control = this.FindControl<Control>(controlName);
        if (control is not null) control.IsVisible = !control.IsVisible;
    }

    private void ShowLocalNotes()
    {
        var version = HubVersion.Current().ToString();
        this.FindControl<TextBlock>("VersionText")!.Text = $"Installed version: {version}";
        this.FindControl<TextBlock>("WhatChangedText")!.Text = _releaseNotes.GetWhatChanged(version);
        this.FindControl<TextBlock>("ReleaseInfoText")!.Text = _releaseNotes.GetReleaseInformation(version);
    }

    private async Task RefreshAsync(bool force)
    {
        _setStatus("Checking for announcements...");
        try
        {
            ShowContent(await _contentService.RefreshAsync(force));
            _setStatus("Announcements up to date.");
        }
        catch (Exception exception)
        {
            // Offline is normal; the cached announcement stays on screen.
            DebugLogService.Info($"Announcement check failed: {exception.Message}");
            _setStatus("Could not reach the announcement feed.");
        }
    }

    private void ShowContent(HubContentResult result)
    {
        var announcement = result.Content.CurrentAnnouncement.Message;
        this.FindControl<TextBlock>("AnnouncementText")!.Text =
            string.IsNullOrWhiteSpace(announcement) ? "No announcement right now." : announcement;

        this.FindControl<TextBlock>("ServiceStatusText")!.Text = result.IsOnline
            ? $"Live. Next check {result.NextCheckUtc?.ToLocalTime():g}."
            : result.IsCached
                ? "Offline — showing the last announcement received."
                : "Offline — using the announcement bundled with this build.";

        var history = _historyService.Record(result.Content);
        this.FindControl<ItemsControl>("HistoryList")!.ItemsSource = history;
        this.FindControl<TextBlock>("NoHistoryText")!.IsVisible = history.Count == 0;
    }
}
