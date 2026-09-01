using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Casualties_Hub.Helpers;

/// <summary>
/// Opt-in search box chrome: placeholder text and a clear button. Both live in
/// the shared TextBox template in App.xaml so every search bar lines them up the
/// same way, instead of each page overlaying its own copy at its own margin.
/// </summary>
public static class TextBoxExtras
{
    /// <summary>Hint shown while the box is empty and not being typed in.</summary>
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder", typeof(string), typeof(TextBoxExtras), new PropertyMetadata(string.Empty));

    public static void SetPlaceholder(DependencyObject element, string value) =>
        element.SetValue(PlaceholderProperty, value);

    public static string GetPlaceholder(DependencyObject element) =>
        (string)element.GetValue(PlaceholderProperty);

    /// <summary>Shows a clear button inside the box whenever the box has text.</summary>
    public static readonly DependencyProperty ShowClearButtonProperty =
        DependencyProperty.RegisterAttached(
            "ShowClearButton", typeof(bool), typeof(TextBoxExtras), new PropertyMetadata(false));

    public static void SetShowClearButton(DependencyObject element, bool value) =>
        element.SetValue(ShowClearButtonProperty, value);

    public static bool GetShowClearButton(DependencyObject element) =>
        (bool)element.GetValue(ShowClearButtonProperty);

    /// <summary>Bound by the template button. The parameter is the templated text box.</summary>
    public static ICommand ClearCommand { get; } = new ClearTextCommand();

    private sealed class ClearTextCommand : ICommand
    {
        // The command is always available; the button itself is only shown when
        // there is text to clear, so there is nothing to re-query.
        event EventHandler? ICommand.CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => parameter is TextBox;

        public void Execute(object? parameter)
        {
            if (parameter is not TextBox box) return;
            // Clear() raises TextChanged, so any search filter wired to the box
            // re-runs on its own and the full list comes straight back.
            box.Clear();
            box.Focus();
        }
    }
}
