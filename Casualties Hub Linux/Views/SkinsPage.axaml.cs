using Avalonia;
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
    private bool _facingBack;
    private bool _panning;
    private Point _panOrigin;
    private Vector _panOffset;

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

        this.FindControl<Button>("TurnAroundButton")!.Click += (_, _) =>
        {
            _facingBack = !_facingBack;
            RenderPreview();
        };

        this.FindControl<Button>("ReturnToCentreButton")!.Click += (_, _) => ReturnToCentre();

        var zoom = this.FindControl<Slider>("ZoomSlider")!;
        zoom.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "Value") RenderPreview();
        };

        // Drag to pan once the art is bigger than the panel. Avalonia has no MouseCapture; a
        // pointer capture on the scroller does the same job.
        var scroller = this.FindControl<ScrollViewer>("PreviewScroller")!;
        scroller.PointerPressed += (_, e) =>
        {
            _panOrigin = e.GetPosition(scroller);
            _panOffset = scroller.Offset;
            _panning = true;
            e.Pointer.Capture(scroller);
        };
        scroller.PointerMoved += (_, e) =>
        {
            if (!_panning) return;
            var now = e.GetPosition(scroller);
            scroller.Offset = new Vector(
                _panOffset.X - (now.X - _panOrigin.X),
                _panOffset.Y - (now.Y - _panOrigin.Y));
        };
        scroller.PointerReleased += (_, e) =>
        {
            _panning = false;
            e.Pointer.Capture(null);
        };
    }

    private void ReturnToCentre()
    {
        this.FindControl<Slider>("ZoomSlider")!.Value = 6;
        this.FindControl<ScrollViewer>("PreviewScroller")!.Offset = default;
        RenderPreview();
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

        _setStatus($"{slots.Count} skin slots found.");
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

        var zoom = Math.Max(1, this.FindControl<Slider>("ZoomSlider")!.Value);
        this.FindControl<TextBlock>("ZoomLabel")!.Text = $"{zoom:F0}x";

        try
        {
            var canvas = SkinPreviewComposer.Compose(_selectedSlotPath, head, eyes, _facingBack);
            if (canvas.Children.Count == 0)
            {
                ClearPreview("This slot has no sprites the preview can draw.");
                return;
            }

            // Size the Viewbox to an exact multiple of the canvas so each source pixel lands on a
            // whole number of screen pixels. Letting it stretch to fill produces fractional
            // scaling, which is what makes pixel art look smeared.
            var host = this.FindControl<Viewbox>("PreviewHost")!;
            host.Width = canvas.Width * zoom;
            host.Height = canvas.Height * zoom;
            host.Child = canvas;

            ShowMissingSprites();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Could not build the skin preview", exception);
            ClearPreview("The preview could not be drawn; see the log.");
        }
    }

    /// <summary>
    /// Names the sprites a slot is missing. A skin can render and still be incomplete, so this is
    /// the only way to tell a deliberate art choice from a file that failed to install.
    /// </summary>
    private void ShowMissingSprites()
    {
        var warning = this.FindControl<TextBlock>("MissingSpriteText")!;
        if (string.IsNullOrWhiteSpace(_selectedSlotPath))
        {
            warning.IsVisible = false;
            return;
        }

        var missing = SkinRig.FindMissingRequiredSprites(_selectedSlotPath);
        warning.IsVisible = missing.Count > 0;
        warning.Text = missing.Count == 0
            ? ""
            : $"Missing {missing.Count} required sprites: {string.Join(", ", missing)}";
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

        var host = this.FindControl<Viewbox>("PreviewHost")!;
        host.Width = double.NaN;
        host.Height = double.NaN;
        host.Child = note;

        this.FindControl<TextBlock>("MissingSpriteText")!.IsVisible = false;
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_root)) LinuxShell.OpenFolder(_root);
    }
}
