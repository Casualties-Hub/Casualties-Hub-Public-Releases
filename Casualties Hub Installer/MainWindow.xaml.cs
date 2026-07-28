using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Casualties_Hub_Installer.Models;
using Casualties_Hub_Installer.Services;
using Forms = System.Windows.Forms;

namespace Casualties_Hub_Installer;

public partial class MainWindow : Window
{
    private readonly GitHubReleaseService _releaseService = new();
    private readonly HubInstallService _installService = new();
    private List<DetectedInstallation> _installations = [];
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();
        Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/CasualtiesHub.png"));
        InstallPathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CasualtiesHub", "Current");
        Loaded += async (_, _) =>
        {
            await LoadReleasesAsync();
            await RefreshKnownInstallationsAsync(scanCommonLocations: true, updateStatus: false);
        };
        InstallPathTextBox.TextChanged += (_, _) => UpdateInstalledVersionLabel();
        _ = RefreshKnownInstallationsAsync(scanCommonLocations: false, updateStatus: false);
    }

    private async Task LoadReleasesAsync()
    {
        if (_loading) return;
        _loading = true;
        SetBusy(true, "Loading official GitHub releases...");
        try
        {
            var releases = await _releaseService.GetReleasesAsync(IncludePrereleasesCheckBox.IsChecked == true);
            ReleaseComboBox.ItemsSource = releases;
            ReleaseComboBox.SelectedItem = releases.FirstOrDefault();
            ReleaseCountText.Text = releases.Count == 1
                ? "1 published GitHub release found"
                : $"{releases.Count} published GitHub releases found";
            StatusText.Text = releases.Count == 0 ? "No installable official ZIP releases were found." : $"Loaded {releases.Count} official release(s).";
        }
        catch (Exception exception)
        {
            ReleaseCountText.Text = "GitHub releases could not be loaded";
            StatusText.Text = "Could not load GitHub releases. Check your internet connection and try again.";
            System.Windows.MessageBox.Show(exception.Message, "GitHub releases unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
            _loading = false;
        }
    }

    private async void RefreshVersions_Click(object sender, RoutedEventArgs e) => await LoadReleasesAsync();
    private async void IncludePrereleasesChanged(object sender, RoutedEventArgs e) => await LoadReleasesAsync();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { Description = "Choose where Casualties Hub should be installed" };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) InstallPathTextBox.Text = dialog.SelectedPath;
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (ReleaseComboBox.SelectedItem is not HubRelease release)
        {
            System.Windows.MessageBox.Show("Choose an official release first.", "No release selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var installPath = InstallPathTextBox.Text.Trim();
        var oldVersion = InstalledHubRegistry.GetInstalledVersion(installPath);
        var action = string.IsNullOrWhiteSpace(oldVersion) ? "install" : $"replace the existing version ({oldVersion})";
        if (System.Windows.MessageBox.Show($"Install {release.Tag} into:\n\n{installPath}\n\nThis will {action} while leaving untracked personal files alone.", "Confirm installation", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            SetBusy(true, "Preparing installation...");
            var progress = new Progress<string>(message => StatusText.Text = message);
            await _installService.InstallAsync(release, installPath, progress);
            await RefreshKnownInstallationsAsync(scanCommonLocations: false, updateStatus: false);
            UpdateInstalledVersionLabel();
            StatusText.Text = $"Installed Casualties Hub {release.Tag}.";
            if (LaunchAfterInstallCheckBox.IsChecked == true) HubInstallService.Launch(installPath);
        }
        catch (Exception exception)
        {
            StatusText.Text = "Installation failed. No existing installation was intentionally removed.";
            System.Windows.MessageBox.Show(exception.Message, "Installation failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private async void DetectInstallations_Click(object sender, RoutedEventArgs e)
    {
        await RefreshKnownInstallationsAsync(scanCommonLocations: true, updateStatus: true);
    }

    private async Task RefreshKnownInstallationsAsync(bool scanCommonLocations, bool updateStatus)
    {
        if (updateStatus) SetBusy(true, "Looking for installed Casualties Hub copies...");
        try
        {
            _installations = (await InstalledHubRegistry.DiscoverAsync(scanCommonLocations))
                .Select(installation => new DetectedInstallation
                {
                    Path = installation.Path,
                    Version = installation.Version,
                    LastInstalledUtc = installation.LastInstalledUtc
                })
                .ToList();
            KnownInstallationsList.ItemsSource = _installations;
            KnownInstallationsEmptyText.Visibility = _installations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateUninstallButton();
            if (updateStatus) StatusText.Text = _installations.Count == 0
                ? "No Casualties Hub installations were found."
                : $"Found {_installations.Count} Casualties Hub installation(s).";
        }
        finally
        {
            if (updateStatus) SetBusy(false, StatusText.Text);
        }
    }

    private void KnownInstallationsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (KnownInstallationsList.SelectedItem is DetectedInstallation installation)
        {
            InstallPathTextBox.Text = installation.Path;
            StatusText.Text = $"Selected {installation.Version} for update or removal.";
        }
    }

    private void InstallationCheckBox_Click(object sender, RoutedEventArgs e) => UpdateUninstallButton();

    private void DeleteHubDataChanged(object sender, RoutedEventArgs e) => UpdateUninstallButton();

    private void SelectAllInstallations_Click(object sender, RoutedEventArgs e)
    {
        foreach (var installation in _installations) installation.IsSelected = true;
        KnownInstallationsList.Items.Refresh();
        UpdateUninstallButton();
    }

    private void ClearInstallationSelection_Click(object sender, RoutedEventArgs e)
    {
        foreach (var installation in _installations) installation.IsSelected = false;
        KnownInstallationsList.Items.Refresh();
        UpdateUninstallButton();
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var selectedInstallations = _installations.Where(installation => installation.IsSelected).ToList();
        if (selectedInstallations.Count == 0)
        {
            System.Windows.MessageBox.Show("Tick at least one detected Hub installation first.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var fullRemoval = DeleteHubDataCheckBox.IsChecked == true;
        var unselectedInstallInsideHubData = fullRemoval
            ? _installations.FirstOrDefault(installation => !installation.IsSelected && HubInstallService.IsInsideHubDataDirectory(installation.Path))
            : null;
        if (unselectedInstallInsideHubData is not null)
        {
            System.Windows.MessageBox.Show(
                $"Full removal would also delete this unselected Hub installation because it is stored inside Hub data:\n\n{unselectedInstallInsideHubData.Path}\n\nTick it too, or turn off full removal.",
                "Select every Hub copy inside Hub data",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }
        var selectedPaths = string.Join("\n", selectedInstallations.Select(installation => $"• {installation.Path}"));
        var extraWarning = fullRemoval
            ? $"\n\nAlso delete all Hub-owned data here:\n{HubInstallService.HubDataDirectory}\n\nThis includes settings, logs, cache, downloaded imports, and Protected Assets."
            : "\n\nYour shared settings, logs, cache, and Protected Assets will stay in place.";

        var confirmation = System.Windows.MessageBox.Show(
            $"Remove {selectedInstallations.Count} selected Casualties Hub installation(s)?\n\n{selectedPaths}\n\nThe Setup Wizard will close Hub processes running from those folders, then delete only the selected Hub folders.{extraWarning}",
            fullRemoval ? "Confirm full Casualties Hub removal" : "Remove selected Casualties Hub copies",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            SetBusy(true, "Closing Casualties Hub and removing selected folders...");
            var progress = new Progress<string>(message => StatusText.Text = message);
            foreach (var installation in selectedInstallations)
                await _installService.UninstallAsync(installation.Path, progress);
            if (fullRemoval)
                await _installService.RemoveHubDataAsync(progress);
            await RefreshKnownInstallationsAsync(scanCommonLocations: false, updateStatus: false);
            UpdateInstalledVersionLabel();
            StatusText.Text = fullRemoval
                ? "Selected Hub copies and Hub data were removed."
                : "Selected Casualties Hub copies were removed.";
        }
        catch (Exception exception)
        {
            StatusText.Text = "Removal failed. The Hub copy was not completely removed.";
            System.Windows.MessageBox.Show(exception.Message, "Removal failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private void UpdateInstalledVersionLabel()
    {
        var version = InstalledHubRegistry.GetInstalledVersion(InstallPathTextBox.Text.Trim());
        InstalledVersionText.Text = string.IsNullOrWhiteSpace(version)
            ? "No existing Casualties Hub installation was found in this folder."
            : $"Detected installed Casualties Hub version: {version}";
    }

    private void SetBusy(bool busy, string status)
    {
        InstallButton.IsEnabled = !busy;
        ReleaseComboBox.IsEnabled = !busy;
        IncludePrereleasesCheckBox.IsEnabled = !busy;
        RefreshVersionsButton.IsEnabled = !busy;
        BrowseButton.IsEnabled = !busy;
        DetectInstallationsButton.IsEnabled = !busy;
        SelectAllInstallationsButton.IsEnabled = !busy;
        ClearInstallationSelectionButton.IsEnabled = !busy;
        DeleteHubDataCheckBox.IsEnabled = !busy;
        UninstallButton.IsEnabled = !busy && _installations.Any(installation => installation.IsSelected);
        BusyProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = status;
    }

    private void UpdateUninstallButton()
    {
        var selectedCount = _installations.Count(installation => installation.IsSelected);
        UninstallButton.IsEnabled = !_loading && selectedCount > 0;
        UninstallButton.Content = DeleteHubDataCheckBox.IsChecked == true
            ? $"Full remove ({selectedCount})"
            : $"Remove selected ({selectedCount})";
    }
}
