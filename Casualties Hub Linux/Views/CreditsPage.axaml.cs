using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Contributor credits.
/// </summary>
/// <remarks>
/// The Windows page hardcodes each card as a block of XAML. This drives them from a list instead,
/// so adding a contributor is one line rather than a copied 20-element element tree. Images load
/// through avares:// rather than WPF's pack:// scheme.
/// </remarks>
public partial class CreditsPage : UserControl
{
    private sealed record Credit(string Name, string Role, string Contribution, string? ImageAsset)
    {
        public Bitmap? Image { get; } = LoadAsset(ImageAsset);
        public bool HasImage => Image is not null;
    }

    public CreditsPage()
    {
        AvaloniaXamlLoader.Load(this);

        this.FindControl<ItemsControl>("CreditList")!.ItemsSource = new List<Credit>
        {
            new("MarlyZ89", "Project Creator",
                "Creator of Casualties Hub & Casualties Setup Wizard", "CreditMarly.png"),
            new("JimmyKing", "Core Library Developer",
                "Creator of Nexus Metadata, QoLUnknown, and CUCoreLib", null),
            new("EXP-3238", "Founding Tester",
                "Tested private builds before the first public release. Creator of the \"Get Zucked\" easter egg.",
                "CreditExp3238.png"),
            new("YOU1ARE0IC", "Community Contributor",
                "Creator of PapaZuck", "CreditYou1Are0Ic.png"),
            new("Linda", "Pre-Alpha Tester",
                "Official bug tester for private pre-alpha builds.", null),
            new("Ares v3.2", "Pre-Alpha Tester",
                "Official bug tester for private pre-alpha builds.", null),
            new("Storm Shirogane", "Pre-Alpha Tester",
                "Official bug tester for private pre-alpha builds.", null),
        };
    }

    private static Bitmap? LoadAsset(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://casualties-hub/Assets/{fileName}"));
            return new Bitmap(stream);
        }
        catch (Exception exception)
        {
            // A missing portrait falls back to the "Pending Picture" panel rather than
            // taking the whole page down.
            DebugLogService.Info($"Credits image {fileName} could not be loaded: {exception.Message}");
            return null;
        }
    }
}
