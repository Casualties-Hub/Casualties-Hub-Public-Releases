using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Picks which CustomSprites st# folder a skin archive should be installed into.
/// </summary>
/// <remarks>
/// Ported from the Windows SkinSlotDialog. The behaviour that matters is unchanged: slots already
/// holding art are listed first and clearly marked, because choosing one silently destroys the
/// sprites in it. Avalonia's ShowDialog is async, so the result is read from a property after the
/// await rather than from DialogResult.
/// </remarks>
public partial class SkinSlotDialog : Window
{
    /// <summary>One entry in the picker: either a slot that already holds art, or a free one.</summary>
    private sealed record SlotChoice(string Name, string Label, bool IsOccupied, int HeadCount, int BodyCount)
    {
        // ComboBox shows this via ToString, which avoids needing a DataTemplate for one string.
        public override string ToString() => Label;
    }

    /// <summary>How many unused slots to offer past the highest one in use, so a skin can go somewhere new.</summary>
    private const int EmptySlotsToOffer = 3;

    private readonly List<SlotChoice> _choices = [];

    public string SelectedSlot { get; private set; } = "st0";

    /// <summary>True when the chosen slot already holds sprites, so the caller can warn before replacing them.</summary>
    public bool SelectedSlotIsOccupied { get; private set; }

    /// <summary>Set only when the user confirmed. Avalonia has no DialogResult on Window.</summary>
    public bool Confirmed { get; private set; }

    public SkinSlotDialog()
    {
        AvaloniaXamlLoader.Load(this);

        BuildChoices();

        var box = this.FindControl<ComboBox>("SlotBox")!;
        box.ItemsSource = _choices;
        box.SelectionChanged += (_, _) => UpdateHint();
        box.SelectedIndex = 0;
        UpdateHint();

        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close();
        this.FindControl<Button>("ConfirmButton")!.Click += (_, _) =>
        {
            if (this.FindControl<ComboBox>("SlotBox")!.SelectedItem is not SlotChoice choice) return;
            SelectedSlot = choice.Name;
            SelectedSlotIsOccupied = choice.IsOccupied;
            Confirmed = true;
            Close();
        };
    }

    private void BuildChoices()
    {
        List<Models.SkinSlot> existing;
        try
        {
            existing = new SkinLibraryService(new SettingsService(), new ModService()).DiscoverSlots();
        }
        catch (Exception exception)
        {
            // A missing or unreadable game folder must not block installing; fall back to a plain list.
            DebugLogService.Error("Skin slots could not be listed for the install dialog", exception);
            existing = [];
        }

        foreach (var slot in existing)
            _choices.Add(new SlotChoice(
                slot.Name,
                $"{slot.Name}   (in use: {slot.HeadSpriteCount} head, {slot.BodySpriteCount} body)",
                true, slot.HeadSpriteCount, slot.BodySpriteCount));

        // Offer the next few unused numbers, always covering at least the classic st0-st9 range.
        var highestUsed = existing.Count == 0 ? -1 : existing.Max(slot => slot.Number);
        var highestToOffer = Math.Max(highestUsed + EmptySlotsToOffer, 9);
        var used = existing.Select(slot => slot.Number).ToHashSet();
        for (var number = 0; number <= highestToOffer; number++)
        {
            if (used.Contains(number)) continue;
            _choices.Add(new SlotChoice($"st{number}", $"st{number}   (empty)", false, 0, 0));
        }
    }

    private void UpdateHint()
    {
        var hint = this.FindControl<TextBlock>("SlotHint")!;
        var confirm = this.FindControl<Button>("ConfirmButton")!;

        if (this.FindControl<ComboBox>("SlotBox")!.SelectedItem is not SlotChoice choice)
        {
            hint.Text = "";
            return;
        }

        hint.Text = choice.IsOccupied
            ? $"Installing here permanently replaces the {choice.HeadCount + choice.BodyCount} sprites already in {choice.Name}."
            : $"{choice.Name} is empty, so nothing will be overwritten.";

        confirm.Content = choice.IsOccupied ? "Replace slot" : "Use slot";
        confirm.Classes.Set("danger", choice.IsOccupied);
        confirm.Classes.Set("accent", !choice.IsOccupied);
    }
}
