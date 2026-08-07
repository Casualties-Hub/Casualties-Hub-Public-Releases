using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// One dashboard card, wrapping a <see cref="MetadataMod"/> for display.
/// </summary>
/// <remarks>
/// The Windows cards bind straight to the model and drive their colours with DataTriggers.
/// Avalonia has no DataTriggers, and the model raises no change notifications, so the icon (which
/// arrives asynchronously) and the expanded/collapsed state would never reach the screen. This
/// wrapper adds the notifications and turns the trigger conditions into plain bound properties,
/// which also keeps UI state off the shared model.
/// </remarks>
public sealed class DashboardCard : INotifyPropertyChanged
{
    private static readonly IBrush Installed = new SolidColorBrush(Color.FromRgb(0x3F, 0x6B, 0x4A));
    private static readonly IBrush OutOfDate = new SolidColorBrush(Color.FromRgb(0xA8, 0x84, 0x2F));
    private static readonly IBrush InstalledText = new SolidColorBrush(Color.FromRgb(0x67, 0xE4, 0x80));
    private static readonly IBrush OutOfDateText = new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x53));
    private static readonly IBrush NeutralBorder = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x38));
    private static readonly IBrush NeutralText = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAF));

    private Bitmap? _icon;
    private bool _isDescriptionExpanded;

    public DashboardCard(MetadataMod mod) => Mod = mod;

    public MetadataMod Mod { get; }

    public string Name => Mod.Name;
    public string Author => Mod.Author;
    public string TotalDownloadsLabel => Mod.TotalDownloadsLabel;
    public string UniqueDownloadsLabel => Mod.UniqueDownloadsLabel;
    public string EndorsementsLabel => Mod.EndorsementsLabel;
    public string VersionLabel => $"Version {Mod.Version}";
    public string FileSizeLabel => Mod.FileSizeLabel;
    public string DependenciesLabel => Mod.DependenciesLabel;
    public string LocalStatusLabel => Mod.LocalStatusLabel;
    public string RenderedDescription => Mod.RenderedDescription;
    public string DashboardActionLabel => Mod.DashboardActionLabel;

    // Out-of-date is checked first so it wins over plain "installed", matching the Windows
    // trigger order where the later DataTrigger takes precedence.
    public IBrush CardBorderBrush =>
        Mod.IsLocallyOutOfDate ? OutOfDate : Mod.IsLocallyInstalled ? Installed : NeutralBorder;

    public IBrush StatusBrush =>
        Mod.IsLocallyOutOfDate ? OutOfDateText : Mod.IsLocallyInstalled ? InstalledText : NeutralText;

    public Bitmap? Icon
    {
        get => _icon;
        private set { _icon = value; Raise(); }
    }

    public bool IsDescriptionExpanded
    {
        get => _isDescriptionExpanded;
        set { _isDescriptionExpanded = value; Raise(); }
    }

    /// <summary>Fetches the mod icon in the background; a failure just leaves the placeholder.</summary>
    public async Task LoadIconAsync() => Icon = await RemoteImageCache.GetAsync(Mod.ImageUrl);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
