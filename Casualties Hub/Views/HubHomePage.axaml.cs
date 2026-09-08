using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Announcements and community links from the published Hub configuration, plus the release
/// notes embedded in this build.
/// </summary>
/// <remarks>
/// The shell owns the configuration service so the Report issues button and this page read the
/// same document. Remote content is treated as untrusted: it only ever becomes TextBlock text,
/// and links go through DesktopShell, which allow-lists the scheme.
/// </remarks>
public partial class HubHomePage : UserControl
{
    private const string ReleasesUrl = "https://github.com/Casualties-Hub/Casualties-Hub-Public-Releases/releases";
    private const string NexusUrl = "https://www.nexusmods.com/casualtiesunknown";
    private const string LinkUnavailableTip = "Link not available.";

    private readonly HubConfigService _configService;
    private readonly ReleaseNotesService _releaseNotes = new();
    private readonly Action<string> _setStatus;
    private readonly Action? _openCredits;
    private readonly Action<HubConfigResult>? _configRefreshed;
    private string? _discordUrl;

    public HubHomePage() : this(_ => { }, null, new HubConfigService(new SettingsService()), null) { }

    public HubHomePage(Action<string> setStatus, Action? openCredits, HubConfigService configService, Action<HubConfigResult>? configRefreshed)
    {
        _setStatus = setStatus;
        _openCredits = openCredits;
        _configService = configService;
        _configRefreshed = configRefreshed;
        AvaloniaXamlLoader.Load(this);

        this.FindControl<Button>("CheckButton")!.Click += async (_, _) => await RefreshAsync(force: true);
        this.FindControl<Button>("HistoryButton")!.Click += (_, _) => Toggle("HistoryCard");
        this.FindControl<Button>("ReleaseInfoButton")!.Click += (_, _) => Toggle("ReleaseInfoText");
        this.FindControl<Button>("ReleasesButton")!.Click += (_, _) => DesktopShell.OpenUrl(ReleasesUrl);
        this.FindControl<Button>("NexusButton")!.Click += (_, _) => DesktopShell.OpenUrl(NexusUrl);
        this.FindControl<Button>("DiscordButton")!.Click += (_, _) => { if (_discordUrl is { } url) DesktopShell.OpenUrl(url); };
        this.FindControl<Button>("CreditsButton")!.Click += (_, _) => _openCredits?.Invoke();

        ShowLocalNotes();
        ShowConfig(_configService.LoadCached());

        // Only reach out if the cached copy is stale, so opening this page is not a network hit.
        if (_configService.IsCheckDue()) _ = RefreshAsync(force: false);
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
            var result = await _configService.RefreshAsync(force);
            ShowConfig(result);
            _configRefreshed?.Invoke(result);
            _setStatus("Announcements up to date.");
        }
        catch (Exception exception)
        {
            // Offline is normal; the cached announcement stays on screen.
            DebugLogService.Info($"Announcement check failed: {exception.Message}");
            _setStatus("Could not reach the Hub configuration.");
        }
    }

    private void ShowConfig(HubConfigResult result)
    {
        this.FindControl<TextBlock>("AnnouncementText")!.Text =
            result.Config.CurrentAnnouncement?.Message ?? "No announcement right now.";

        this.FindControl<TextBlock>("ServiceStatusText")!.Text = result.IsOnline
            ? $"Live. Next check {result.NextCheckUtc?.ToLocalTime():g}."
            : "Offline — showing the last configuration received.";

        var previous = result.Config.PreviousAnnouncements;
        this.FindControl<ItemsControl>("HistoryList")!.ItemsSource = previous;
        this.FindControl<TextBlock>("NoHistoryText")!.IsVisible = previous.Count == 0;

        _discordUrl = string.IsNullOrWhiteSpace(result.Config.Links.DiscordUrl) ? null : result.Config.Links.DiscordUrl;
        var discordButton = this.FindControl<Button>("DiscordButton")!;
        discordButton.IsEnabled = _discordUrl is not null;
        ToolTip.SetTip(discordButton, _discordUrl is null ? LinkUnavailableTip : null);
    }
}
