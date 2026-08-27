using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Casualties_Hub.Models;

namespace Casualties_Hub.Views;

public partial class UninstallDialog : Window
{
    private readonly List<(UninstallItem Item, CheckBox Box)> _entries;

    public UninstallDialog(IReadOnlyList<UninstallItem> items)
    {
        InitializeComponent();
        _entries = items.Select(item => (item, CreateCheckBox(item))).ToList();
        foreach (var (_, box) in _entries) ItemsPanel.Children.Add(box);
        UpdateWarning();
    }

    public IReadOnlyList<UninstallItem> SelectedItems { get; private set; } = [];

    private CheckBox CreateCheckBox(UninstallItem item)
    {
        var box = new CheckBox
        {
            IsChecked = item.IsSelected,
            Margin = new Thickness(0, 0, 0, 10),
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = item.Title, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold },
                    new TextBlock { Text = item.Description, Foreground = new SolidColorBrush(Color.FromRgb(0xA9, 0xAF, 0xB8)), TextWrapping = TextWrapping.Wrap, FontSize = 12 }
                }
            }
        };
        box.Checked += (_, _) => UpdateWarning();
        box.Unchecked += (_, _) => UpdateWarning();
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

    private void ConfirmationBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateWarning();

    private void UpdateWarning()
    {
        var selected = _entries.Where(entry => entry.Box.IsChecked == true).Select(entry => entry.Item).ToList();
        WarningText.Text = selected.Count == 0
            ? "Nothing is selected. Choose at least one item to remove."
            : $"This will permanently delete: {string.Join(", ", selected.Select(item => item.Title))}.";
        UninstallButton.IsEnabled = selected.Count > 0 && string.Equals(ConfirmationBox.Text.Trim(), "DELETE", StringComparison.OrdinalIgnoreCase);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        SelectedItems = _entries.Where(entry => entry.Box.IsChecked == true).Select(entry => entry.Item).ToList();
        DialogResult = true;
    }
}
