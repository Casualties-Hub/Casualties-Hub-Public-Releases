using Avalonia.Media;
using Casualties_Hub.Models;

namespace Casualties_Hub.Views;

/// <summary>
/// One card in the Local Mods columns, wrapping an <see cref="InstalledMod"/> for display.
/// </summary>
/// <remarks>
/// The Windows card drives its colours with DataTriggers. Avalonia has none, so each trigger
/// condition becomes a bound property here. WPF applies the LAST matching trigger, so the
/// precedence below is deliberately reversed relative to the order they appear in ModsPage.xaml.
/// Keeping this out of <see cref="InstalledMod"/> also keeps Avalonia types out of the model.
/// </remarks>
public sealed class ModRow
{
    // Taken verbatim from the Windows CompactModCard so both editions read identically.
    private static readonly IBrush DisabledEdge = Parse("#7A3038");
    private static readonly IBrush ModlistEdge = Parse("#6A4A94");
    private static readonly IBrush NeutralEdge = Parse("#2A2F38");
    private static readonly IBrush UpToDateText = Parse("#67E480");
    private static readonly IBrush OutOfDateText = Parse("#F1C453");
    private static readonly IBrush DisabledText = Parse("#D58B8B");
    private static readonly IBrush NeutralText = Parse("#F1EFEE");

    private static IBrush Parse(string hex) => new SolidColorBrush(Color.Parse(hex));

    public ModRow(InstalledMod mod) => Mod = mod;

    public InstalledMod Mod { get; }

    public string Name => Mod.Name;
    public string VersionLabel => Mod.VersionLabel;
    public string VersionEvidenceLabel => Mod.VersionEvidenceLabel;
    public string UpdateStatusLabel => Mod.UpdateStatusLabel;
    public string ToggleButtonLabel => Mod.ToggleButtonLabel;
    public string DependencyActionLabel => Mod.DependencyActionLabel;
    public string ShareCodeActionLabel => Mod.ShareCodeActionLabel;
    public string MissingDependenciesLabel => Mod.MissingDependenciesLabel;
    public string IncompatibilityLabel => Mod.IncompatibilityLabel;
    public string KnownBugsLabel => Mod.KnownBugsLabel;
    public string? DependencyRequiredByLabel => Mod.DependencyRequiredByLabel;

    public bool HasMissingDependencies => Mod.HasMissingDependencies;
    public bool HasIncompatibilities => Mod.HasIncompatibilities;
    public bool HasKnownBugs => Mod.HasKnownBugs;
    public bool IsDependencyPlaceholder => Mod.IsDependencyPlaceholder;
    public bool IsMissingFromModlist => Mod.IsMissingFromModlist;

    /// <summary>
    /// True for cards backed by real files. A dependency placeholder or a share-code entry the
    /// player does not have is listed for information only, so Enable and Delete are hidden.
    /// </summary>
    public bool IsManageable => !Mod.IsDependencyPlaceholder && !Mod.IsMissingFromModlist;

    /// <summary>The Windows MultiDataTrigger: offer the update link only for a mod that is actually running.</summary>
    public bool ShowOutOfDateAction => Mod.IsOutOfDate && !Mod.IsDisabled;

    public IBrush CardBorderBrush =>
        Mod.IsMissingFromModlist ? ModlistEdge
        : Mod.IsDisabled ? DisabledEdge
        : NeutralEdge;

    public IBrush StatusBrush =>
        Mod.IsDisabled ? DisabledText
        : Mod.IsOutOfDate ? OutOfDateText
        : Mod.IsUpToDate ? UpToDateText
        : NeutralText;
}
