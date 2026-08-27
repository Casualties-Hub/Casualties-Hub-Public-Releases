using System.Windows;
using System.Windows.Controls;
using Casualties_Hub.Models;

namespace Casualties_Hub.Views;

public partial class HubHomePage : Page
{
    private readonly Func<HubHomeState> _getState;
    private readonly Action _openReleaseHistory;
    private readonly Action _openNexusPage;
    private readonly Action _openDiscord;
    private readonly Action _openCredits;

    public HubHomePage(
        Func<HubHomeState> getState,
        Action openReleaseHistory,
        Action openNexusPage,
        Action openDiscord,
        Action openCredits)
    {
        _getState = getState;
        _openReleaseHistory = openReleaseHistory;
        _openNexusPage = openNexusPage;
        _openDiscord = openDiscord;
        _openCredits = openCredits;
        InitializeComponent();
        Loaded += (_, _) => RefreshView();
    }

    public void RefreshView()
    {
        var state = _getState();
        CurrentVersionText.Text = $"Installed version: {state.CurrentVersion}";
        InstallationDetailText.Text = state.ReleaseInformation;
        CurrentAnnouncementText.Text = state.CurrentAnnouncement;
        WhatChangedText.Text = state.WhatChangedText;
        ReleaseInformationText.Text = state.ReleaseInformation;
        AnnouncementHistoryList.ItemsSource = state.AnnouncementHistory;
        NoHistoryText.Visibility = state.AnnouncementHistory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PriorAnnouncements_Click(object sender, RoutedEventArgs e)
        => AnnouncementHistoryCard.Visibility = Toggle(AnnouncementHistoryCard.Visibility);

    private void ReleaseInformation_Click(object sender, RoutedEventArgs e)
        => ReleaseInformationPanel.Visibility = Toggle(ReleaseInformationPanel.Visibility);

    private static Visibility Toggle(Visibility visibility)
        => visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void ReleaseHistory_Click(object sender, RoutedEventArgs e) => _openReleaseHistory();
    private void NexusPage_Click(object sender, RoutedEventArgs e) => _openNexusPage();
    private void OpenDiscord_Click(object sender, RoutedEventArgs e) => _openDiscord();
    private void OpenCredits_Click(object sender, RoutedEventArgs e) => _openCredits();
}
