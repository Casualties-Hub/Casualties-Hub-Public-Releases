using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Checklist of Hub data to remove, handed to the shell helper that does the deleting.
/// </summary>
/// <remarks>
/// The helper cannot run while the Hub is holding its own files open, so confirming here starts
/// the script and immediately shuts the app down. <see cref="Confirmed"/> tells the caller to do
/// that, since Avalonia has no DialogResult.
/// </remarks>
public partial class UninstallDialog : Window
{
    private readonly List<UninstallItem> _items;

    /// <summary>True when the user confirmed; the caller must then close the application.</summary>
    public bool Confirmed { get; private set; }

    public UninstallDialog() : this(new SettingsService()) { }

    public UninstallDialog(SettingsService settingsService)
    {
        AvaloniaXamlLoader.Load(this);

        _items = UninstallService.GetItems(settingsService).ToList();

        // Nothing is ticked by default. The Windows dialog pre-selects everything, which is a
        // poor default for an action that cannot be undone.
        foreach (var item in _items) item.IsSelected = false;

        this.FindControl<ItemsControl>("ItemList")!.ItemsSource = _items;
        this.FindControl<TextBlock>("WarningText")!.Text =
            "Removal happens after the Hub closes and cannot be stopped once it starts.";

        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close();
        this.FindControl<Button>("RemoveButton")!.Click += async (_, _) => await ConfirmAsync();
    }

    private async Task ConfirmAsync()
    {
        var selected = _items.Where(item => item.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await HubDialog.ShowMessageAsync(this, "Nothing selected", "Tick at least one item to remove.");
            return;
        }

        // Show the resolved paths. The checklist titles are friendly but vague, and this is the
        // last point at which a misconfigured settings file can be spotted before deletion.
        var paths = UninstallService.ResolveDeletablePaths(selected);
        if (paths.Count == 0)
        {
            await HubDialog.ShowMessageAsync(this, "Nothing to remove",
                "None of the selected items resolved to a path the Hub is allowed to delete.");
            return;
        }

        var body = "These will be deleted permanently:\n\n"
                   + string.Join("\n", paths.Select(path => "  " + path))
                   + "\n\nThe Hub will close immediately after starting the removal.";

        if (!await HubDialog.ConfirmAsync(this, $"Remove {paths.Count} item(s)?", body,
                confirm: "Remove and quit", destructive: true))
            return;

        try
        {
            UninstallService.BeginUninstall(selected);
            Confirmed = true;
            Close();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not start the uninstall helper", exception);
            await HubDialog.ShowMessageAsync(this, "Could not start removal", exception.Message);
        }
    }
}
