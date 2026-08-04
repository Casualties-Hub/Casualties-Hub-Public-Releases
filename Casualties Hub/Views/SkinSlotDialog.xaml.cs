using System.Windows;
using System.Windows.Controls;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

public partial class SkinSlotDialog : Window
{
    /// <summary>One entry in the slot picker: either a slot that already holds art, or the next free slot.</summary>
    private sealed record SlotChoice(string Name, string Label, bool IsOccupied, int HeadCount, int BodyCount);

    /// <summary>How many unused slots to offer past the highest one in use, so a skin can go somewhere new.</summary>
    private const int EmptySlotsToOffer = 3;

    private readonly List<SlotChoice> _choices = [];

    public string SelectedSlot { get; private set; } = "st0";

    /// <summary>True when the chosen slot already contains sprites, so callers can warn about replacing them.</summary>
    public bool SelectedSlotIsOccupied { get; private set; }

    public SkinSlotDialog()
    {
        InitializeComponent();
        BuildChoices();
        SlotBox.ItemsSource = _choices;
        SlotBox.SelectedIndex = 0;
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
            // A missing or unreadable game folder must not block installing; fall back to the plain st0-st9 list.
            DebugLogService.Error("Skin slots could not be listed for the install dialog", exception);
            existing = [];
        }

        foreach (var slot in existing)
            _choices.Add(new SlotChoice(slot.Name, $"{slot.Name}   (in use: {slot.HeadSpriteCount} head, {slot.BodySpriteCount} body)", true, slot.HeadSpriteCount, slot.BodySpriteCount));

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

    private void SlotBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SlotBox.SelectedItem is not SlotChoice choice)
        {
            SlotHint.Text = "";
            return;
        }
        SlotHint.Text = choice.IsOccupied
            ? $"Installing here permanently replaces the {choice.HeadCount + choice.BodyCount} sprite(s) already in {choice.Name}. Protect the folder first if you want to keep them."
            : $"{choice.Name} is empty, so nothing will be overwritten.";
        ConfirmButton.Content = choice.IsOccupied ? "Replace slot" : "Use slot";
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (SlotBox.SelectedItem is not SlotChoice choice) return;
        SelectedSlot = choice.Name;
        SelectedSlotIsOccupied = choice.IsOccupied;
        DialogResult = true;
    }
}
