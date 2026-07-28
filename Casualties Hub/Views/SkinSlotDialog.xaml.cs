using System.Windows;
using System.Windows.Controls;

namespace Casualties_Hub.Views;

public partial class SkinSlotDialog : Window
{
    public string SelectedSlot { get; private set; } = "st0";

    public SkinSlotDialog() => InitializeComponent();

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        SelectedSlot = (SlotBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "st0";
        DialogResult = true;
    }
}
