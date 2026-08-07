using Avalonia.Media;
using Casualties_Hub.Models;

namespace Casualties_Hub.Views;

/// <summary>
/// One row in the Local Mods list, wrapping an <see cref="InstalledMod"/> for display.
/// </summary>
/// <remarks>
/// The Windows list colours rows with DataTriggers, which Avalonia does not have. Rather than
/// scatter converters through the XAML, the trigger conditions become plain bound properties here.
/// Keeping them off <see cref="InstalledMod"/> also keeps Avalonia types out of the model.
/// </remarks>
public sealed class ModRow
{
    private static readonly IBrush Missing = new SolidColorBrush(Color.FromRgb(0xB4, 0x8E, 0xFF));
    private static readonly IBrush OutOfDate = new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x53));
    private static readonly IBrush Danger = new SolidColorBrush(Color.FromRgb(0xC8, 0x1E, 0x3C));
    private static readonly IBrush Neutral = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
    private static readonly IBrush NeutralBorder = new SolidColorBrush(Color.FromRgb(0x2A, 0x2F, 0x38));

    public ModRow(InstalledMod mod) => Mod = mod;

    public InstalledMod Mod { get; }

    public string Name => Mod.Name;
    public string VersionLabel => Mod.VersionLabel;
    public string VersionEvidenceLabel => Mod.VersionEvidenceLabel;
    public string UpdateStatusLabel => Mod.UpdateStatusLabel;
    public string ToggleButtonLabel => Mod.ToggleButtonLabel;
    public string DependencyActionLabel => Mod.DependencyActionLabel;
    public string RequiredDependenciesLabel => Mod.RequiredDependenciesLabel;
    public string MissingDependenciesLabel => Mod.MissingDependenciesLabel;
    public string IncompatibilityLabel => Mod.IncompatibilityLabel;
    public string KnownBugsLabel => Mod.KnownBugsLabel;
    public string? DependencyRequiredByLabel => Mod.DependencyRequiredByLabel;

    public bool IsDisabled => Mod.IsDisabled;
    public bool IsOutOfDate => Mod.IsOutOfDate;
    public bool IsDependencyPlaceholder => Mod.IsDependencyPlaceholder;
    public bool HasRequiredDependencies => Mod.HasRequiredDependencies;
    public bool HasMissingDependencies => Mod.HasMissingDependencies;
    public bool HasIncompatibilities => Mod.HasIncompatibilities;
    public bool HasKnownBugs => Mod.HasKnownBugs;

    /// <summary>
    /// True for rows backed by files on disk. A dependency placeholder or a modlist entry the
    /// player does not have is listed for information only, so enable and delete would have
    /// nothing to act on.
    /// </summary>
    public bool IsManageable => !Mod.IsDependencyPlaceholder && !Mod.IsMissingFromModlist;

    public IBrush NameBrush => Mod.IsMissingFromModlist ? Missing : Neutral;

    public IBrush RowBorderBrush =>
        Mod.HasIncompatibilities ? Danger
        : Mod.IsMissingFromModlist ? Missing
        : Mod.IsOutOfDate || Mod.HasMissingDependencies ? OutOfDate
        : NeutralBorder;
}
