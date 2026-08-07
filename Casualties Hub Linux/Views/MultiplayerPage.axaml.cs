using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Casualties_Hub.Models;
using Casualties_Hub.Services;

namespace Casualties_Hub.Views;

/// <summary>
/// Layout preview for the planned server browser. Every row is sample data built locally; the
/// page makes no network request and Join is inert until a real multiplayer service exists.
/// </summary>
/// <remarks>
/// Ported from the Windows page, with one addition: the fake data is called out in the interface
/// rather than only in a source comment. A tester cannot read the comment, and a list of servers
/// that looks real but cannot be joined reads as a bug.
/// </remarks>
public partial class MultiplayerPage : UserControl
{
    private static readonly (string Code, string Label)[] Regions =
    [
        ("nae", "NA East"),
        ("naw", "NA West"),
        ("euw", "EU West"),
        ("eue", "EU East"),
        ("sa", "South America"),
        ("as", "Asia"),
        ("oce", "Oceania"),
    ];

    private static readonly (string Label, int Value)[] PingOptions =
    [
        ("Any ping", 0),
        ("50 ms or less", 50),
        ("100 ms or less", 100),
        ("150 ms or less", 150),
    ];

    private readonly Action<string> _setStatus;
    private readonly List<MultiplayerServer> _allServers = BuildSampleServers();
    private bool _initialised;

    public MultiplayerPage() : this(_ => { }) { }

    public MultiplayerPage(Action<string> setStatus)
    {
        _setStatus = setStatus;
        AvaloniaXamlLoader.Load(this);

        var regionBox = this.FindControl<ComboBox>("RegionBox")!;
        regionBox.ItemsSource = Regions.Select(region => region.Label).ToList();
        regionBox.SelectedIndex = 0;

        var pingBox = this.FindControl<ComboBox>("PingBox")!;
        pingBox.ItemsSource = PingOptions.Select(option => option.Label).ToList();
        pingBox.SelectedIndex = 0;

        _initialised = true;

        regionBox.SelectionChanged += (_, _) => ApplyFilters();
        pingBox.SelectionChanged += (_, _) => ApplyFilters();
        this.FindControl<CheckBox>("HideLockedBox")!.IsCheckedChanged += (_, _) => ApplyFilters();
        this.FindControl<TextBox>("SearchBox")!.TextChanged += (_, _) => ApplyFilters();
        this.FindControl<Button>("RefreshButton")!.Click += (_, _) =>
        {
            // Nothing to re-fetch: the rows are built locally. Re-applying the filters keeps the
            // button honest rather than having it pretend to contact a service.
            ApplyFilters();
            _setStatus("Sample data reloaded. There is no multiplayer service to query yet.");
        };

        ApplyFilters();
        DebugLogService.Activity("Multiplayer", "Opened the multiplayer server browser preview.");
    }

    private void ApplyFilters()
    {
        if (!_initialised) return;

        var regionCode = Regions[Math.Max(this.FindControl<ComboBox>("RegionBox")!.SelectedIndex, 0)].Code;
        var maxPing = PingOptions[Math.Max(this.FindControl<ComboBox>("PingBox")!.SelectedIndex, 0)].Value;
        var hideLocked = this.FindControl<CheckBox>("HideLockedBox")!.IsChecked == true;
        var search = this.FindControl<TextBox>("SearchBox")!.Text?.Trim() ?? "";

        var visible = _allServers.Where(server => server.RegionCode == regionCode);
        if (maxPing > 0) visible = visible.Where(server => server.Ping <= maxPing);
        if (hideLocked) visible = visible.Where(server => !server.Locked);
        if (search.Length > 0)
            visible = visible.Where(server => server.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        var rows = visible.OrderBy(server => server.Ping).ToList();
        this.FindControl<ItemsControl>("ServerList")!.ItemsSource = rows;
        this.FindControl<TextBlock>("EmptyText")!.IsVisible = rows.Count == 0;
    }

    private void OnJoin(object? sender, RoutedEventArgs e)
    {
        var name = ((sender as Button)?.Tag as MultiplayerServer)?.Name ?? "that server";
        _setStatus($"'{name}' is sample data — there is no multiplayer service to join yet.");
    }

    private static List<MultiplayerServer> BuildSampleServers() =>
    [
        new() { Name = "Ashfall Outpost",   RegionCode = "nae", Layer = 1, Mood = "Casual",     Population = 12, Capacity = 24, Ping = 28,  ModCount = 6 },
        new() { Name = "Quarantine Line",   RegionCode = "nae", Layer = 2, Mood = "Hardcore",   Population = 22, Capacity = 24, Ping = 41,  Locked = true, ModCount = 14 },
        new() { Name = "Rust Belt Relay",   RegionCode = "naw", Layer = 1, Mood = "Casual",     Population = 5,  Capacity = 16, Ping = 63,  ModCount = 3 },
        new() { Name = "Nightshift",        RegionCode = "naw", Layer = 3, Mood = "Roleplay",   Population = 9,  Capacity = 12, Ping = 77,  ModCount = 21 },
        new() { Name = "Greyhaven",         RegionCode = "euw", Layer = 1, Mood = "Casual",     Population = 18, Capacity = 32, Ping = 34,  ModCount = 8 },
        new() { Name = "Blackout Sector",   RegionCode = "euw", Layer = 2, Mood = "Hardcore",   Population = 30, Capacity = 32, Ping = 52,  Locked = true, ModCount = 17 },
        new() { Name = "Cold Storage",      RegionCode = "eue", Layer = 1, Mood = "Casual",     Population = 7,  Capacity = 24, Ping = 88,  ModCount = 4 },
        new() { Name = "Meridian Station",  RegionCode = "sa",  Layer = 2, Mood = "Roleplay",   Population = 14, Capacity = 20, Ping = 121, ModCount = 11 },
        new() { Name = "Monsoon Yard",      RegionCode = "as",  Layer = 1, Mood = "Casual",     Population = 20, Capacity = 24, Ping = 96,  ModCount = 5 },
        new() { Name = "Longshore",         RegionCode = "oce", Layer = 1, Mood = "Casual",     Population = 3,  Capacity = 16, Ping = 143, ModCount = 2 },
    ];
}
