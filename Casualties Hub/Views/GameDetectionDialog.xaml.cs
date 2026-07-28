using System.Windows;

namespace Casualties_Hub.Views;

public partial class GameDetectionDialog : Window
{
    public bool OpenSettingsRequested { get; private set; }

    public GameDetectionDialog() => InitializeComponent();

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsRequested = false;
        Close();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettingsRequested = true;
        Close();
    }
}
