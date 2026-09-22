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
/// The model raises no change notifications, so the icon (which arrives asynchronously) and the
/// expanded/collapsed state would never reach the screen. This wrapper adds them, and keeps UI
/// state off the shared model.
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

    // Compact forms for the row and tile layouts, where a full "53,728 total downloads" line
    // would not fit.
    public string AuthorAndVersion => $"{Mod.Author} · v{Mod.Version}";
    public string CompactDownloads => Compact(Mod.TotalDownloads);
    public string CompactUniqueDownloads => Compact(Mod.UniqueDownloads);
    public string CompactEndorsements => Compact(Mod.Endorsements);
    public bool HasDependencies => Mod.DependenciesLabel.StartsWith("Requires", StringComparison.Ordinal);

    /// <summary>The first sentence or so of the description, flattened to one line.</summary>
    public string Excerpt
    {
        get
        {
            var text = Mod.RenderedDescription.Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (text.Contains("  ", StringComparison.Ordinal)) text = text.Replace("  ", " ", StringComparison.Ordinal);
            return text.Length > 160 ? text[..157].TrimEnd() + "..." : text;
        }
    }

    public string RowSubtitle => $"{Mod.Author} · {Excerpt}";

    public string TileSubtitle => $"{Mod.Author} · {CompactDownloads} downloads";

    public bool HasStatusChip => Mod.IsLocallyInstalled;

    public string StatusChipLabel => Mod.IsLocallyDisabled
        ? "Disabled"
        : Mod.IsLocallyOutOfDate ? "Update available" : "Installed";

    private static string Compact(int value) => value switch
    {
        >= 1_000_000 => $"{value / 1_000_000d:0.#}M",
        >= 1_000 => $"{value / 1_000d:0.#}k",
        _ => value.ToString(),
    };

    // Out-of-date is checked first so it wins over plain "installed".
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
        set { _isDescriptionExpanded = value; Raise(); Raise(nameof(IsCollapsed)); }
    }

    /// <summary>Inverse of <see cref="IsDescriptionExpanded"/>, for parts that hide when open.</summary>
    public bool IsCollapsed => !_isDescriptionExpanded;

    /// <summary>Fetches the mod icon in the background; a failure just leaves the placeholder.</summary>
    public async Task LoadIconAsync() => Icon = await RemoteImageCache.GetAsync(Mod.ImageUrl);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
