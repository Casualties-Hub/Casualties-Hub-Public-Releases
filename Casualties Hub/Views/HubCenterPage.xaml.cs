using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Casualties_Hub.Models;

namespace Casualties_Hub.Views;

public partial class HubCenterPage : Page
{
    private readonly Func<HubCenterState> _getState;
    private readonly Func<bool, Task> _setOnlineServicesEnabled;
    private readonly Func<Task> _checkServiceNow;
    private readonly Func<Task> _installUpdate;
    private readonly Func<Task> _installLocalZip;
    private readonly Action _openReleaseHistory;
    private readonly Action _openDiscord;

    public HubCenterPage(
        Func<HubCenterState> getState,
        Func<bool, Task> setOnlineServicesEnabled,
        Func<Task> checkServiceNow,
        Func<Task> installUpdate,
        Func<Task> installLocalZip,
        Action openReleaseHistory,
        Action openDiscord)
    {
        _getState = getState;
        _setOnlineServicesEnabled = setOnlineServicesEnabled;
        _checkServiceNow = checkServiceNow;
        _installUpdate = installUpdate;
        _installLocalZip = installLocalZip;
        _openReleaseHistory = openReleaseHistory;
        _openDiscord = openDiscord;
        InitializeComponent();
        Loaded += (_, _) => RefreshView();
    }

    public void RefreshView()
    {
        var state = _getState();
        CurrentVersionText.Text = $"Installed version: {state.CurrentVersion}";
        CurrentAnnouncementText.Text = state.OnlineServicesEnabled
            ? state.CurrentAnnouncement
            : "Online services are disabled. Enable them to receive announcements and update checks.";
        WhatChangedText.Text = state.WhatChangedText;
        AnnouncementHistoryList.ItemsSource = state.AnnouncementHistory;
        NoHistoryText.Visibility = state.AnnouncementHistory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CheckNowButton.IsEnabled = state.OnlineServicesEnabled && state.ManualCheckAvailable;
        CheckNowButton.Opacity = CheckNowButton.IsEnabled ? 1 : 0.45;
        CheckNowButton.ToolTip = !state.OnlineServicesEnabled
            ? "Online services are disabled. Enable them before checking."
            : state.ManualCheckAvailable
                ? "Request GitHub announcements and release information now. Available once every 30 minutes."
                : state.NextManualCheckUtc is { } nextManual
                    ? $"Check now is cooling down. Available at {nextManual.LocalDateTime:t}."
                    : "Check now is temporarily unavailable.";
        ActivitySummaryText.Text = state.OnlineServicesEnabled
            ? "Privacy: public GitHub files are checked without sending an installation ID or activity metrics."
            : "GitHub content checks are currently disabled.";
        var statusLegend = "Status colors:\nGreen — GitHub was reached.\nRed — using saved data because GitHub could not be reached.\nGray — online services are turned off.";
        ServiceStateDot.ToolTip = statusLegend;
        ServiceStateText.ToolTip = statusLegend;

        if (!state.OnlineServicesEnabled)
        {
            ServiceStateDot.Fill = new SolidColorBrush(Color.FromRgb(130, 130, 130));
            ServiceStateText.Text = "Offline by choice";
            ServiceDetailText.Text = "Online services are disabled. No GitHub content or update requests will be sent.";
            ToggleServicesButton.Content = "Enable online services";
            InstallUpdateButton.Visibility = Visibility.Collapsed;
            UpdateText.Text = "Automatic update checks are paused.";
            return;
        }

        ServiceStateDot.Fill = state.ServiceOnline
                ? new SolidColorBrush(Color.FromRgb(45, 190, 90))
                : new SolidColorBrush(Color.FromRgb(183, 28, 42));
        ServiceStateText.Text = state.ServiceOnline ? "Connected" : "Using saved data";
        ServiceDetailText.Text = state.ServiceOnline
            ? state.ShowingCachedServiceData && state.NextServiceCheckUtc is { } next
                ? $"GitHub reports no changes. Next normal check: {next.LocalDateTime:g}."
                : "Downloaded updated Casualties Hub content from GitHub."
            : state.NextServiceCheckUtc is { } retry
                ? $"The service could not be reached. Saved data remains available. Next retry: {retry.LocalDateTime:g}."
                : "The service could not be reached. Saved data remains available.";
        ToggleServicesButton.Content = "Disable online services";
        UpdateText.Text = state.UpdateAvailable
            ? $"Update available: {state.UpdateVersion}."
            : $"No eligible update is currently available. {state.ReleaseInformation}";
        InstallUpdateButton.Visibility = state.UpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
        InstallUpdateButton.Content = state.UpdateAvailable ? "Install update" : "Install update";
    }

    private async void ToggleServices_Click(object sender, RoutedEventArgs e)
    {
        var enable = !_getState().OnlineServicesEnabled;
        ToggleServicesButton.IsEnabled = false;
        try { await _setOnlineServicesEnabled(enable); }
        finally { ToggleServicesButton.IsEnabled = true; RefreshView(); }
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        try { await _checkServiceNow(); }
        finally { CheckNowButton.IsEnabled = true; RefreshView(); }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        await _installUpdate();
        RefreshView();
    }

    private async void InstallLocalZip_Click(object sender, RoutedEventArgs e)
    {
        await _installLocalZip();
        RefreshView();
    }

    private void ReleaseHistory_Click(object sender, RoutedEventArgs e) => _openReleaseHistory();
    private void OpenDiscord_Click(object sender, RoutedEventArgs e) => _openDiscord();
}
