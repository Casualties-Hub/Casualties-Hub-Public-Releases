using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Casualties_Hub.Models;

namespace Casualties_Hub.Views;

public partial class UpdateCleanupDialog : Window
{
    private readonly List<(UpdateStagingFolder Folder, CheckBox Box)> _entries;

    public UpdateCleanupDialog(IReadOnlyList<UpdateStagingFolder> folders)
    {
        InitializeComponent();
        _entries = folders.Select(folder => (folder, CreateCheckBox(folder))).ToList();
        foreach (var (_, box) in _entries) ItemsPanel.Children.Add(box);
        UpdateSummary();
    }

    public IReadOnlyList<string> SelectedPaths { get; private set; } = [];

    private CheckBox CreateCheckBox(UpdateStagingFolder folder)
    {
        var box = new CheckBox
        {
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 10),
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = $"{folder.Kind} — {folder.DisplaySize}", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold },
                    new TextBlock { Text = $"Last used {folder.LastWriteTimeUtc.ToLocalTime():g}", Foreground = new SolidColorBrush(Color.FromRgb(0xA9, 0xAF, 0xB8)), FontSize = 12 }
                }
            },
            ToolTip = folder.Path
        };
        box.Checked += (_, _) => UpdateSummary();
        box.Unchecked += (_, _) => UpdateSummary();
        return box;
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (_, box) in _entries) box.IsChecked = true;
    }

    private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (_, box) in _entries) box.IsChecked = false;
    }

    private void UpdateSummary()
    {
        var selected = _entries.Where(entry => entry.Box.IsChecked == true).Select(entry => entry.Folder).ToList();
        var totalBytes = selected.Sum(folder => folder.SizeBytes);
        SummaryText.Text = selected.Count == 0
            ? "Nothing selected."
            : $"{selected.Count} folder(s) selected, about {FormatSize(totalBytes)}.";
        DeleteButton.IsEnabled = selected.Count > 0;
    }

    private static string FormatSize(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / (1024.0 * 1024.0):0.#} MB"
        : $"{bytes / 1024.0:0.#} KB";

    private void Skip_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        SelectedPaths = _entries.Where(entry => entry.Box.IsChecked == true).Select(entry => entry.Folder.Path).ToList();
        DialogResult = true;
    }
}
