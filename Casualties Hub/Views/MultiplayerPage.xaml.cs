using System.Windows;
using System.Windows.Controls;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Layout preview for the planned server browser. Every row is sample data
/// built locally; the page makes no network request and Join is inert until a
/// real multiplayer service exists.
/// </summary>
public partial class MultiplayerPage : Page
{
    private static readonly (string Code, string Label)[] Regions =
    [
        ("nae", "NA East"),
        ("naw", "NA West"),
        ("euw", "EU West"),
        ("eue", "EU East"),
        ("sa", "South America"),
        ("as", "Asia"),
        ("oce", "Oceania")
    ];

    private static readonly (string Label, int Value)[] PingOptions =
    [
        ("Any ping", 0),
        ("50 ms or less", 50),
        ("100 ms or less", 100),
        ("150 ms or less", 150)
    ];

    private readonly Action<string> _setStatus;
    private readonly List<MultiplayerServer> _allServers = BuildSampleServers();
    private bool _initialized;

    public MultiplayerPage(Action<string> setStatus)
    {
        _setStatus = setStatus;
        InitializeComponent();

        RegionBox.ItemsSource = Regions.Select(region => region.Label).ToList();
        RegionBox.SelectedIndex = 0;
        PingBox.ItemsSource = PingOptions.Select(option => option.Label).ToList();
        PingBox.SelectedIndex = 0;
        _initialized = true;

        Loaded += (_, _) => ApplyFilters();
        DebugLogService.Activity("Multiplayer", "Opened the multiplayer server browser preview.");
    }

    private void ApplyFilters()
    {
        var regionCode = Regions[Math.Max(RegionBox.SelectedIndex, 0)].Code;
        var maxPing = PingOptions[Math.Max(PingBox.SelectedIndex, 0)].Value;
        var hideLocked = HideLockedBox.IsChecked == true;
        var search = SearchBox.Text?.Trim() ?? "";

        var visible = _allServers.Where(server => server.RegionCode == regionCode);
        if (maxPing > 0) visible = visible.Where(server => server.Ping <= maxPing);
        if (hideLocked) visible = visible.Where(server => !server.Locked);
        if (search.Length > 0)
            visible = visible.Where(server => server.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        var rows = visible.OrderByDescending(server => server.Population).ToList();
        ServerGrid.ItemsSource = rows;

        var regionLabel = Regions[Math.Max(RegionBox.SelectedIndex, 0)].Label;
        ResultsText.Text = $"Showing {rows.Count} sample server{(rows.Count == 1 ? "" : "s")} in {regionLabel}.";
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        // The ComboBoxes raise SelectionChanged while the constructor is still
        // populating them, before the other controls have been touched.
        if (!_initialized) return;
        ApplyFilters();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ApplyFilters();
        _setStatus("Multiplayer is a preview. The list was rebuilt from sample data, not from a server.");
    }

    private void Join_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Multiplayer is not connected yet.\n\nThis page is a layout preview for a future release, so there is no server to join.",
            "Multiplayer preview",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        DebugLogService.Activity("Multiplayer", "Join was clicked on the preview server list.");
    }

    private static List<MultiplayerServer> BuildSampleServers() =>
    [
        new() { Name = "Ashfall Outpost", RegionCode = "nae", Layer = 1, Mood = "Casual", Population = 18, Capacity = 24, Ping = 32, ModCount = 4 },
        new() { Name = "Quarantine Zone 7", RegionCode = "nae", Layer = 2, Mood = "Hardcore", Locked = true, Population = 22, Capacity = 24, Ping = 48, ModCount = 9 },
        new() { Name = "The Long Shift", RegionCode = "nae", Layer = 1, Mood = "Roleplay", Population = 11, Capacity = 16, Ping = 61, ModCount = 6 },
        new() { Name = "Rustbelt Relay", RegionCode = "nae", Layer = 3, Mood = "Casual", Population = 7, Capacity = 20, Ping = 84, ModCount = 2 },
        new() { Name = "Pacific Holdout", RegionCode = "naw", Layer = 1, Mood = "Casual", Population = 20, Capacity = 24, Ping = 41, ModCount = 5 },
        new() { Name = "Silt Harbor", RegionCode = "naw", Layer = 2, Mood = "Hardcore", Population = 14, Capacity = 16, Ping = 77, ModCount = 8 },
        new() { Name = "Greyline Depot", RegionCode = "euw", Layer = 1, Mood = "Casual", Population = 23, Capacity = 24, Ping = 38, ModCount = 3 },
        new() { Name = "Chapel of Rust", RegionCode = "euw", Layer = 2, Mood = "Roleplay", Locked = true, Population = 15, Capacity = 20, Ping = 52, ModCount = 11 },
        new() { Name = "Nordkap Station", RegionCode = "eue", Layer = 1, Mood = "Hardcore", Population = 9, Capacity = 16, Ping = 95, ModCount = 7 },
        new() { Name = "Cinder Bay", RegionCode = "sa", Layer = 1, Mood = "Casual", Population = 12, Capacity = 24, Ping = 128, ModCount = 4 },
        new() { Name = "Kanto Fallback", RegionCode = "as", Layer = 2, Mood = "Casual", Population = 19, Capacity = 24, Ping = 143, ModCount = 6 },
        new() { Name = "Southern Cross", RegionCode = "oce", Layer = 1, Mood = "Roleplay", Population = 8, Capacity = 16, Ping = 156, ModCount = 5 }
    ];
}
