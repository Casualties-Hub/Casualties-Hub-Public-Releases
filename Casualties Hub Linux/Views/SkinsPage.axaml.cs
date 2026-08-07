using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Lists the CustomSprites slots found in the plugins folder and renders a preview of one.
/// </summary>
public partial class SkinsPage : UserControl
{
    /// <summary>Combo entry that shows a friendly label but carries the enum value.</summary>
    private sealed record Choice<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly SkinLibraryService _skinLibrary;
    private readonly Action<string> _setStatus;

    private string _root = "";
    private string? _selectedSlotPath;

    public SkinsPage() : this(_ => { }) { }

    public SkinsPage(Action<string> setStatus)
    {
        _setStatus = setStatus;
        _skinLibrary = new SkinLibraryService(new SettingsService(), new ModService());
        AvaloniaXamlLoader.Load(this);

        BuildPoseControls();

        this.FindControl<Button>("RefreshButton")!.Click += (_, _) => Reload();
        this.FindControl<Button>("OpenFolderButton")!.Click += OnOpenFolder;

        Reload();
    }

    private void BuildPoseControls()
    {
        var headBox = this.FindControl<ComboBox>("HeadBox")!;
        headBox.ItemsSource = new[]
        {
            new Choice<SkinHeadShape>(SkinHeadShape.Normal, "Normal"),
            new Choice<SkinHeadShape>(SkinHeadShape.NormalMouthOpen, "Mouth open"),
            new Choice<SkinHeadShape>(SkinHeadShape.NormalMouthHalf, "Mouth half"),
            new Choice<SkinHeadShape>(SkinHeadShape.Disfigured1, "Disfigured 1"),
            new Choice<SkinHeadShape>(SkinHeadShape.Disfigured1Healed, "Disfigured 1 healed"),
            new Choice<SkinHeadShape>(SkinHeadShape.Disfigured2, "Disfigured 2"),
            new Choice<SkinHeadShape>(SkinHeadShape.Disfigured2Healed, "Disfigured 2 healed"),
            new Choice<SkinHeadShape>(SkinHeadShape.Disfigured3, "Disfigured 3"),
            new Choice<SkinHeadShape>(SkinHeadShape.Disfigured3Healed, "Disfigured 3 healed"),
        };
        headBox.SelectedIndex = 0;

        var eyeBox = this.FindControl<ComboBox>("EyeBox")!;
        eyeBox.ItemsSource = SkinRig.AvailableEyeExpressions()
            .Select(expression => new Choice<SkinEyeExpression>(expression, Humanise(expression.ToString())))
            .ToList();
        eyeBox.SelectedIndex = 1; // Open, the pose a skin is usually judged on.

        headBox.SelectionChanged += (_, _) => RenderPreview();
        eyeBox.SelectionChanged += (_, _) => RenderPreview();
        this.FindControl<CheckBox>("FacingBox")!.IsCheckedChanged += (_, _) => RenderPreview();
    }

    /// <summary>"HalfClosed" -> "Half closed".</summary>
    private static string Humanise(string enumName)
    {
        var spaced = string.Concat(enumName.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
        return spaced;
    }

    private void Reload()
    {
        List<SkinSlot> slots = [];
        try
        {
            _root = _skinLibrary.GetCustomSpritesRoot();
            slots = _skinLibrary.DiscoverSlots();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not read the skins folder", exception);
            _setStatus("Could not read the skins folder; see the log.");
        }

        this.FindControl<TextBlock>("RootPathText")!.Text =
            string.IsNullOrWhiteSpace(_root) ? "No plugins folder configured." : _root;

        this.FindControl<ItemsControl>("SlotList")!.ItemsSource = slots;

        var empty = this.FindControl<TextBlock>("EmptyText")!;
        empty.IsVisible = slots.Count == 0;
        empty.Text = string.IsNullOrWhiteSpace(_root) || !Directory.Exists(_root)
            ? "No CustomSprites folder was found. Install a skin mod first, or set your game folder in Settings."
            : "No skin slots contain any sprites yet.";

        this.FindControl<Button>("OpenFolderButton")!.IsEnabled = Directory.Exists(_root);

        // Show the first slot straight away rather than an empty panel.
        if (slots.Count > 0) ShowSlot(slots[0]);
        else ClearPreview("Select a skin slot to preview it.");

        _setStatus($"{slots.Count} skin slot(s) found.");
    }

    private void OnPreview(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is SkinSlot slot) ShowSlot(slot);
    }

    private void ShowSlot(SkinSlot slot)
    {
        _selectedSlotPath = slot.FolderPath;
        this.FindControl<TextBlock>("PreviewSlotText")!.Text = $"{slot.Name} — {slot.SpriteSummary}";
        RenderPreview();
    }

    private void RenderPreview()
    {
        if (string.IsNullOrWhiteSpace(_selectedSlotPath)) return;

        var head = (this.FindControl<ComboBox>("HeadBox")!.SelectedItem as Choice<SkinHeadShape>)?.Value ?? SkinHeadShape.Normal;
        var eyes = (this.FindControl<ComboBox>("EyeBox")!.SelectedItem as Choice<SkinEyeExpression>)?.Value ?? SkinEyeExpression.Open;
        var facingBack = this.FindControl<CheckBox>("FacingBox")!.IsChecked == true;

        try
        {
            var canvas = SkinPreviewComposer.Compose(_selectedSlotPath, head, eyes, facingBack);
            if (canvas.Children.Count == 0)
            {
                ClearPreview("This slot has no sprites the preview can draw.");
                return;
            }
            this.FindControl<Viewbox>("PreviewHost")!.Child = canvas;
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not build the skin preview", exception);
            ClearPreview("The preview could not be drawn; see the log.");
        }
    }

    private void ClearPreview(string message)
    {
        var note = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Width = 220,
        };
        note.Classes.Add("dim");
        this.FindControl<Viewbox>("PreviewHost")!.Child = note;
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_root)) LinuxShell.OpenFolder(_root);
    }
}
