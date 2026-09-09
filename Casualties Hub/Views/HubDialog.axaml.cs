using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;

namespace Casualties_Hub.Views;

/// <summary>
/// The Hub's replacement for MessageBox, which Avalonia does not provide.
/// </summary>
/// <remarks>
/// Every method here is async, because Avalonia's ShowDialog is. Anything that asks the user a
/// question has to be awaited, so callers are written async from the start.
/// </remarks>
public partial class HubDialog : Window
{
    private bool _result;

    public HubDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Tells the user something. One dismiss button.</summary>
    public static Task ShowMessageAsync(Window owner, string heading, string body, string dismiss = "OK")
    {
        var dialog = Create(heading, body);
        dialog.AddButton(dismiss, isPrimary: true, result: true);
        return dialog.ShowDialog(owner);
    }

    /// <summary>Asks a yes/no question. Returns true only if the user picked the confirm action.</summary>
    public static async Task<bool> ConfirmAsync(
        Window owner, string heading, string body, string confirm = "Continue", string cancel = "Cancel", bool destructive = false)
    {
        var dialog = Create(heading, body);
        // Cancel first so it takes focus: the safe choice should be the one an accidental
        // Enter keypress lands on, especially when the other button deletes files.
        dialog.AddButton(cancel, isPrimary: false, result: false);
        dialog.AddButton(confirm, isPrimary: true, result: true, destructive: destructive);
        await dialog.ShowDialog(owner);
        return dialog._result;
    }

    private static HubDialog Create(string heading, string body)
    {
        var dialog = new HubDialog();
        dialog.FindControl<TextBlock>("HeadingText")!.Text = heading;

        var bodyText = dialog.FindControl<TextBlock>("BodyText")!;
        bodyText.Text = body;
        bodyText.IsVisible = !string.IsNullOrWhiteSpace(body);

        return dialog;
    }

    private void AddButton(string label, bool isPrimary, bool result, bool destructive = false)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        if (destructive) button.Classes.Add("danger");
        else if (isPrimary) button.Classes.Add("accent");

        button.Click += (_, _) =>
        {
            _result = result;
            Close();
        };

        this.FindControl<StackPanel>("ButtonRow")!.Children.Add(button);
        if (!isPrimary) button.Focus();
    }
}
