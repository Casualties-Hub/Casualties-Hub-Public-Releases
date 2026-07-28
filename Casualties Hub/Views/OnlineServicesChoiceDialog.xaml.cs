using System.Windows;

namespace Casualties_Hub.Views;

public partial class OnlineServicesChoiceDialog : Window
{
    public OnlineServicesChoiceDialog() => InitializeComponent();

    private void EnableOnlineServices_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void KeepOffline_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
