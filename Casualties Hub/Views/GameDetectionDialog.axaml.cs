using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Casualties_Hub.Views;

/// <summary>
/// Shown once when the Hub starts and cannot find the game.
/// </summary>
/// <remarks>
/// Avalonia has no DialogResult, so the caller reads <see cref="OpenSettingsRequested"/> after
/// awaiting ShowDialog.
/// </remarks>
public partial class GameDetectionDialog : Window
{
    public bool OpenSettingsRequested { get; private set; }

    public GameDetectionDialog()
    {
        AvaloniaXamlLoader.Load(this);

        this.FindControl<Button>("IgnoreButton")!.Click += (_, _) => Close();
        this.FindControl<Button>("OpenSettingsButton")!.Click += (_, _) =>
        {
            OpenSettingsRequested = true;
            Close();
        };
    }
}
