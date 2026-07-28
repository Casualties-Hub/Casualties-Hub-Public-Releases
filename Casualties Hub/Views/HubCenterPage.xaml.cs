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
    private readonly Action _openReleaseHistory;
    private readonly Action _openDiscord;

    public HubCenterPage(
        Func<HubCenterState> getState,
        Func<bool, Task> setOnlineServicesEnabled,
        Func<Task> checkServiceNow,
        Func<Task> installUpdate,
        Action openReleaseHistory,
        Action openDiscord)
    {
        _getState = getState;
        _setOnlineServicesEnabled = setOnlineServicesEnabled;
        _checkServiceNow = checkServiceNow;
        _installUpdate = installUpdate;
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
                ? "Request the latest Hub status now. Available once every 15 minutes."
                : state.NextManualCheckUtc is { } nextManual
                    ? $"Check now is cooling down. Available at {nextManual.LocalDateTime:t}."
                    : "Check now is temporarily unavailable.";
        ActivitySummaryText.Text = state.OnlineServicesEnabled
            ? $"Community activity: {state.ActiveUsersLastTwoHours} active in 2h · {state.ActiveUsersLastDay} in 24h · {state.ActiveUsersLastWeek} in 7d"
            : "Community activity is hidden while online services are disabled.";
        var statusLegend = "Status colors:\nGreen — connected to Hub services.\nYellow — Hub services are under maintenance.\nRed — using saved data because Hub services could not be reached.\nGray — online services are turned off in Settings.";
        ServiceStateDot.ToolTip = statusLegend;
        ServiceStateText.ToolTip = statusLegend;

        if (!state.OnlineServicesEnabled)
        {
            ServiceStateDot.Fill = new SolidColorBrush(Color.FromRgb(130, 130, 130));
            ServiceStateText.Text = "Offline by choice";
            ServiceDetailText.Text = "Online services are disabled in Settings. No Supabase or GitHub update requests will be sent.";
            ToggleServicesButton.Content = "Enable online services";
            InstallUpdateButton.Visibility = Visibility.Collapsed;
            UpdateText.Text = "Automatic update checks are paused.";
            return;
        }

        ServiceStateDot.Fill = state.ServiceInMaintenance
            ? new SolidColorBrush(Color.FromRgb(230, 165, 35))
            : state.ServiceOnline
                ? new SolidColorBrush(Color.FromRgb(45, 190, 90))
                : new SolidColorBrush(Color.FromRgb(183, 28, 42));
        ServiceStateText.Text = state.ServiceInMaintenance ? "Under maintenance" : state.ServiceOnline ? "Connected" : "Using saved data";
        ServiceDetailText.Text = state.ServiceInMaintenance
            ? "Server under maintenance. We'll be back as soon as possible. Saved announcements remain available."
            : state.ServiceOnline
            ? state.ShowingCachedServiceData && state.NextServiceCheckUtc is { } next
                ? $"Using recently saved service data. Next normal check: {next.LocalDateTime:g}."
                : "Connected to the Casualties Hub update service."
            : state.NextServiceCheckUtc is { } retry
                ? $"The service could not be reached. Saved data remains available. Next retry: {retry.LocalDateTime:g}."
                : "The service could not be reached. Saved data remains available.";
        ToggleServicesButton.Content = "Disable online services";
        UpdateText.Text = state.UpdateAvailable
            ? $"Update available: {state.UpdateVersion}."
            : "No eligible update is currently available.";
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

    private void ReleaseHistory_Click(object sender, RoutedEventArgs e) => _openReleaseHistory();
    private void OpenDiscord_Click(object sender, RoutedEventArgs e) => _openDiscord();
}
