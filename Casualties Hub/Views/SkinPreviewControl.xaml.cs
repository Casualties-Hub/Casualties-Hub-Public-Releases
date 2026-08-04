using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class SkinPreviewControl : UserControl
{
    /// <summary>Space left around the character when the view is fitted, in device pixels.</summary>
    private const double FitPadding = 32;

    private static readonly Dictionary<SkinHeadShape, string> HeadShapeLabels = new()
    {
        [SkinHeadShape.Normal] = "Normal",
        [SkinHeadShape.NormalMouthOpen] = "Normal (mouth open)",
        [SkinHeadShape.NormalMouthHalf] = "Normal (mouth half)",
        [SkinHeadShape.Disfigured1] = "Disfigured I",
        [SkinHeadShape.Disfigured1Healed] = "Disfigured I (Healed)",
        [SkinHeadShape.Disfigured2] = "Disfigured II",
        [SkinHeadShape.Disfigured2Healed] = "Disfigured II (Healed)",
        [SkinHeadShape.Disfigured3] = "Disfigured III",
        [SkinHeadShape.Disfigured3Healed] = "Disfigured III (Healed)",
    };

    private static readonly Dictionary<SkinEyeExpression, string> EyeExpressionLabels = new()
    {
        [SkinEyeExpression.None] = "None",
        [SkinEyeExpression.Open] = "Open",
        [SkinEyeExpression.Closed] = "Closed",
        [SkinEyeExpression.HalfClosed] = "Half-Closed",
        [SkinEyeExpression.Happy] = "Happy",
        [SkinEyeExpression.Sad] = "Sad",
        [SkinEyeExpression.Scared] = "Scared",
        [SkinEyeExpression.Panic] = "Panic",
        [SkinEyeExpression.Gone] = "Missing",
        [SkinEyeExpression.GoneHealed] = "Missing (Healed)",
    };

    private string? _slotFolderPath;
    private bool _facingBack;
    private bool _suppressEvents;

    private Size _composedSize;
    private Point _dragOrigin;
    private double _dragScrollX;
    private double _dragScrollY;
    private bool _isDragging;

    public SkinPreviewControl()
    {
        InitializeComponent();
        PopulateOptions();
        ShowEmptyState();
        // Attached here rather than in XAML: the slider raises ValueChanged while the markup is still
        // being parsed (Minimum coerces Value), at which point controls declared after it are null.
        ZoomSlider.ValueChanged += ZoomSlider_ValueChanged;
        ApplyZoom(ZoomSlider.Value);
    }

    private void PopulateOptions()
    {
        _suppressEvents = true;

        HeadShapeBox.ItemsSource = HeadShapeLabels;
        HeadShapeBox.DisplayMemberPath = "Value";
        HeadShapeBox.SelectedValuePath = "Key";
        HeadShapeBox.SelectedValue = SkinHeadShape.Normal;

        EyeExpressionBox.ItemsSource = SkinPreviewComposer.AvailableEyeExpressions()
            .Select(expression => new KeyValuePair<SkinEyeExpression, string>(expression, EyeExpressionLabels[expression]))
            .ToList();
        EyeExpressionBox.DisplayMemberPath = "Value";
        EyeExpressionBox.SelectedValuePath = "Key";
        EyeExpressionBox.SelectedValue = SkinEyeExpression.Open;

        _suppressEvents = false;
    }

    /// <summary>Loads a CustomSprites st# folder (the one containing Head/ and Body/) and fits it in the frame facing forward.</summary>
    public void LoadSkin(string slotFolderPath)
    {
        _slotFolderPath = slotFolderPath;
        _facingBack = false;
        TurnAroundButton.Content = "Turn Around";
        Render();
        // Deferred: when the page is being constructed the scroll viewer has not been laid out yet,
        // so its viewport is still zero and a fit computed now would be discarded.
        Dispatcher.BeginInvoke(new Action(FitToFrame), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void Clear()
    {
        _slotFolderPath = null;
        MissingSpriteWarning.Visibility = Visibility.Collapsed;
        ShowEmptyState();
    }

    private void ShowEmptyState()
    {
        PreviewHost.Content = null;
        _composedSize = default;
        EmptyPreviewMessage.Visibility = Visibility.Visible;
    }

    private void TurnAround_Click(object sender, RoutedEventArgs e)
    {
        _facingBack = !_facingBack;
        TurnAroundButton.Content = _facingBack ? "Turn Forward" : "Turn Around";
        Render();
    }

    private void Option_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        Render();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ApplyZoom(e.NewValue);

    private void ApplyZoom(double zoom)
    {
        PreviewScale.ScaleX = zoom;
        PreviewScale.ScaleY = zoom;
        ZoomLabel.Text = $"{zoom:0.0}x";
    }

    private void ReturnToCenter_Click(object sender, RoutedEventArgs e) => FitToFrame();

    /// <summary>Picks the largest zoom that keeps the whole character visible, then centres it.</summary>
    private void FitToFrame()
    {
        if (_composedSize.Width <= 0 || _composedSize.Height <= 0) return;

        var availableWidth = PreviewScroller.ViewportWidth - FitPadding;
        var availableHeight = PreviewScroller.ViewportHeight - FitPadding;
        // Before the first layout pass the viewport is still zero; the SizeChanged handler retries.
        if (availableWidth <= 0 || availableHeight <= 0) return;

        var zoom = Math.Min(availableWidth / _composedSize.Width, availableHeight / _composedSize.Height);
        ZoomSlider.Value = Math.Clamp(zoom, ZoomSlider.Minimum, ZoomSlider.Maximum);
        CenterScroll();
    }

    private void CenterScroll()
    {
        // Layout has to settle at the new zoom before the scrollable extent is known.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            PreviewScroller.ScrollToHorizontalOffset(PreviewScroller.ScrollableWidth / 2);
            PreviewScroller.ScrollToVerticalOffset(PreviewScroller.ScrollableHeight / 2);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void PreviewScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Fit once the viewport becomes real, and again whenever the window is resized.
        if (PreviewHost.Content is not null) FitToFrame();
    }

    private void PreviewScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (PreviewHost.Content is null) return;
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + (e.Delta > 0 ? 1 : -1), ZoomSlider.Minimum, ZoomSlider.Maximum);
        e.Handled = true;
    }

    private void PreviewScroller_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (PreviewHost.Content is null) return;
        _dragOrigin = e.GetPosition(PreviewScroller);
        _dragScrollX = PreviewScroller.HorizontalOffset;
        _dragScrollY = PreviewScroller.VerticalOffset;
        _isDragging = true;
        PreviewScroller.CaptureMouse();
    }

    private void PreviewScroller_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var current = e.GetPosition(PreviewScroller);
        // Dragging right moves the artwork right, so the scroll offset goes the opposite way.
        PreviewScroller.ScrollToHorizontalOffset(_dragScrollX - (current.X - _dragOrigin.X));
        PreviewScroller.ScrollToVerticalOffset(_dragScrollY - (current.Y - _dragOrigin.Y));
    }

    private void PreviewScroller_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        PreviewScroller.ReleaseMouseCapture();
    }

    private void Render()
    {
        if (_slotFolderPath is null)
        {
            ShowEmptyState();
            return;
        }

        try
        {
            var canvas = SkinPreviewComposer.Compose(_slotFolderPath, CurrentHeadShape(), CurrentEyeExpression(), _facingBack);
            PreviewHost.Content = canvas;
            _composedSize = new Size(canvas.Width, canvas.Height);
            EmptyPreviewMessage.Visibility = Visibility.Collapsed;
            ShowMissingSpriteWarning();
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Skin preview could not be composed", exception);
            ShowEmptyState();
            EmptyPreviewMessage.Text = "This skin's sprites could not be read.";
        }
    }

    private SkinHeadShape CurrentHeadShape() => (SkinHeadShape)(HeadShapeBox.SelectedValue ?? SkinHeadShape.Normal);
    private SkinEyeExpression CurrentEyeExpression() => (SkinEyeExpression)(EyeExpressionBox.SelectedValue ?? SkinEyeExpression.None);

    private void ShowMissingSpriteWarning()
    {
        var missing = SkinPreviewComposer.FindMissingRequiredSprites(_slotFolderPath!);
        if (missing.Count == 0)
        {
            MissingSpriteWarning.Visibility = Visibility.Collapsed;
            return;
        }
        MissingSpriteText.Text = $"Incorrect or missing textures, re-install. This slot has no {string.Join(", ", missing)}, so {(missing.Count == 1 ? "that part is" : "those parts are")} left out of the preview.";
        MissingSpriteWarning.Visibility = Visibility.Visible;
    }
}
