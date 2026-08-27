using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Casualties_Hub.Models;
using Casualties_Hub.Services;
using Casualties_Hub.Views;

namespace Casualties_Hub;

public partial class MainWindow : Window
{
    private const string ReportIssuesUrl = "https://github.com/Casualties-Hub/Casualties-Hub-Public-Releases/issues";
    private const string SurpriseUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    private readonly SettingsService _settingsService = new();
    private readonly GameInstallDetector _detector = new();
    private readonly GameLaunchService _launchService = new();
    private readonly DownloadImportService _downloadImport = new();

    private Button? _activeNav;
    private int _mascotClicks;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        SetUpWindowIcon();
        SetUpTitleBar();

        var settings = _settingsService.Load();
        ThemeApplier.Apply(settings);
        // Deferred: MainWindow is not yet the lifetime's MainWindow while its constructor runs,
        // so setting FontSize now would apply to nothing.
        Opened += (_, _) => ThemeApplier.ApplyTextSize(_settingsService.Load());

        this.FindControl<TextBlock>("SidebarFooterText")!.Text = $"v{HubVersion.Current()}";

        var dashboardNav = this.FindControl<Button>("DashboardNav")!;
        var modsNav = this.FindControl<Button>("ModsNav")!;
        var multiplayerNav = this.FindControl<Button>("MultiplayerNav")!;
        var skinsNav = this.FindControl<Button>("SkinsNav")!;
        var homeNav = this.FindControl<Button>("HomeNav")!;
        var settingsNav = this.FindControl<Button>("SettingsNav")!;

        void OpenSettings() => Navigate(new SettingsPage(SetStatus), settingsNav, "Settings");
        // Credits has no sidebar entry on Windows either; Hub Home links to it.
        void OpenCredits() => Navigate(new CreditsPage(), homeNav, "Credits");

        // A fresh page per click, matching the Windows Hub. Pages read settings in their
        // constructor, so rebuilding is also how a change made in Settings shows up elsewhere.
        dashboardNav.Click += (_, _) => Navigate(new DashboardPage(SetStatus, OpenSettings), dashboardNav, "Nexus Dashboard");
        modsNav.Click += (_, _) => Navigate(new ModsPage(SetStatus), modsNav, "Local Mods");
        multiplayerNav.Click += (_, _) => Navigate(new MultiplayerPage(SetStatus), multiplayerNav, "Multiplayer");
        skinsNav.Click += (_, _) => Navigate(new SkinsAndBackupsPage(SetStatus), skinsNav, "Skins & Backups");
        homeNav.Click += (_, _) => Navigate(new HubHomePage(SetStatus, OpenCredits), homeNav, "Hub Home");
        settingsNav.Click += (_, _) => OpenSettings();

        this.FindControl<Button>("LaunchGameButton")!.Click += (_, _) => LaunchGame();
        this.FindControl<Button>("ReportIssuesButton")!.Click += (_, _) => LinuxShell.OpenUrl(ReportIssuesUrl);

        SetUpMascot(settings);

        // Windows opens on the Nexus Dashboard.
        Navigate(new DashboardPage(SetStatus, OpenSettings), dashboardNav, "Nexus Dashboard");

        // Restores the sweep if it was left on. It only drives the accent brush, so the player's
        // saved colours survive a restart either way.
        AnimatedRgbDriver.Sync(_settingsService);

        StartDownloadWatcher();

        if (string.IsNullOrWhiteSpace(settings.GamePath)) _ = AutoDetectAsync(OpenSettings);

        Closed += (_, _) =>
        {
            AnimatedRgbDriver.Stop();
            _downloadImport.Dispose();
        };
    }

    private void Navigate(UserControl page, Button navButton, string title)
    {
        this.FindControl<ContentControl>("PageHost")!.Content = page;

        // Avalonia styles off pseudo-classes and style classes rather than WPF's triggers, so
        // "which nav button is active" is expressed by adding and removing a class.
        _activeNav?.Classes.Remove("active");
        navButton.Classes.Add("active");
        _activeNav = navButton;

        DebugLogService.Activity("Navigation", $"Opened {title}.");
    }

    private void LaunchGame()
    {
        try
        {
            _launchService.LaunchViaSteam(_settingsService.Load());
            SetStatus("Asked Steam to launch Casualties Unknown.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Launch failed", exception);
            SetStatus($"Could not launch: {exception.Message}");
        }
    }

    private async Task AutoDetectAsync(Action openSettings)
    {
        SetStatus("Looking for your Casualties Unknown install...");
        try
        {
            var found = await _detector.FindGameInstallAsync(TimeSpan.FromSeconds(20));
            if (string.IsNullOrWhiteSpace(found))
            {
                SetStatus("No Casualties Unknown install found. Set the folder in Settings.");

                // Shown once per launch, and only when nothing was found. Without it a failed
                // detection is just an empty dashboard with no explanation.
                var dialog = new GameDetectionDialog();
                await dialog.ShowDialog(this);
                if (dialog.OpenSettingsRequested) openSettings();
                return;
            }

            var settings = _settingsService.Load();
            settings.GamePath = found;
            _settingsService.Save(settings);
            SetStatus("Game folder detected.");
        }
        catch (Exception exception)
        {
            DebugLogService.Error("Automatic detection failed", exception);
            SetStatus("Detection failed; see the log.");
        }
    }

    // --- download watcher --------------------------------------------------

    private void StartDownloadWatcher()
    {
        // The service runs on a pool thread and cannot show dialogs itself, so it asks through
        // this callback, which hops to the UI thread and awaits the answer.
        _downloadImport.DecideAsync = (plan, archivePath) =>
            Dispatcher.UIThread.InvokeAsync(() => PromptForDownloadAsync(plan, archivePath));

        _downloadImport.ImportCompleted += () => Dispatcher.UIThread.Post(() =>
            SetStatus("A downloaded mod was installed."));

        _downloadImport.Start();
    }

    private async Task<(bool Install, string? SkinSlot)> PromptForDownloadAsync(ArchiveInstallPlan plan, string archivePath)
    {
        var name = System.IO.Path.GetFileName(archivePath);

        // Focus is requested, never forced: the Topmost toggle the Windows Hub uses to steal
        // focus is ignored on Wayland by design, so there is no point imitating it.
        Activate();

        if (plan.Kind == ArchiveInstallKind.Unsupported)
        {
            await HubDialog.ShowMessageAsync(this, "Nothing installable in that download",
                $"'{name}' finished downloading, but {plan.Description}");
            return (false, null);
        }

        var body = plan.Description
                   + (plan.ExistingFilesToReplace.Count > 0
                       ? $"\n\n{plan.ExistingFilesToReplace.Count} existing file(s) will be replaced."
                       : "")
                   + plan.DependencyPrompt;

        if (!await HubDialog.ConfirmAsync(this, $"Install the download {name}?", body, confirm: "Install"))
            return (false, null);

        if (!plan.RequiresSkinSlot) return (true, null);

        var picker = new SkinSlotDialog();
        await picker.ShowDialog(this);
        if (!picker.Confirmed) return (false, null);

        if (picker.SelectedSlotIsOccupied
            && !await HubDialog.ConfirmAsync(this,
                $"Replace the sprites in {picker.SelectedSlot}?",
                $"{picker.SelectedSlot} already contains a skin. Installing over it permanently deletes those sprites.",
                confirm: "Replace", destructive: true))
            return (false, null);

        return (true, picker.SelectedSlot);
    }

    private void SetUpWindowIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://casualties-hub/Assets/CasualtiesHub.png"));
            Icon = new WindowIcon(new Bitmap(stream));
        }
        catch (Exception exception)
        {
            DebugLogService.Info($"Window icon could not be loaded: {exception.Message}");
        }
    }

    private void SetUpTitleBar()
    {
        var maximiseButton = this.FindControl<Button>("MaximiseButton")!;

        foreach (var bar in (Border[])[this.FindControl<Border>("TitleBar")!, this.FindControl<Border>("SidebarBar")!])
        {
            var dragSource = bar;
            dragSource.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(dragSource).Properties.IsLeftButtonPressed) return;
                if (e.Source is Button or TextBox) return;

                if (e.ClickCount == 2) ToggleMaximised();
                else BeginMoveDrag(e);
            };
        }

        this.FindControl<Button>("MinimiseButton")!.Click += (_, _) => WindowState = WindowState.Minimized;
        maximiseButton.Click += (_, _) => ToggleMaximised();
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => Close();

        void SyncMaximiseGlyph()
        {
            var maximised = WindowState == WindowState.Maximized;
            maximiseButton.Content = maximised ? "" : "";
            ToolTip.SetTip(maximiseButton, maximised ? "Restore" : "Maximise");
        }

        SyncMaximiseGlyph();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) SyncMaximiseGlyph();
        };
    }

    private void ToggleMaximised() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // --- mascot ------------------------------------------------------------

    /// <summary>
    /// The PapaZuck easter egg: three clicks pop, five open a surprise.
    /// </summary>
    /// <remarks>
    /// One deliberate difference from Windows. The WPF version plays SystemSounds.Beep on the pop,
    /// which has no cross-platform equivalent and no sensible Linux stand-in: the terminal bell is
    /// usually muted, and pulling in an audio stack for one joke is not worth the dependency.
    /// The animation is the same.
    /// </remarks>
    private void SetUpMascot(Settings settings)
    {
        var mascot = this.FindControl<Image>("PapaZuck")!;
        if (!settings.EasterEggsEnabled) return;

        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://casualties-hub/Assets/PapaZuck.png"));
            mascot.Source = new Bitmap(stream);
            mascot.IsVisible = true;
            mascot.RenderTransform = new ScaleTransform(1, 1);
            mascot.PointerPressed += (_, _) => OnMascotClicked(mascot);
        }
        catch (Exception exception)
        {
            DebugLogService.Info($"PapaZuck could not be loaded: {exception.Message}");
        }
    }

    private async void OnMascotClicked(Image mascot)
    {
        _mascotClicks++;

        if (_mascotClicks % 5 == 0)
        {
            LinuxShell.OpenUrl(SurpriseUrl);
            return;
        }

        if (_mascotClicks % 3 != 0) return;

        // WPF drives this with BeginAnimation, which Avalonia does not have; an Animation run
        // against the control does the same 90ms pop.
        var pop = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(90),
            Easing = new QuadraticEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1d), new Setter(ScaleTransform.ScaleYProperty, 1d) } },
                new KeyFrame { Cue = new Cue(0.5), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1.3), new Setter(ScaleTransform.ScaleYProperty, 1.3) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(ScaleTransform.ScaleXProperty, 1d), new Setter(ScaleTransform.ScaleYProperty, 1d) } },
            },
        };

        try { await pop.RunAsync(mascot); }
        catch (Exception exception) { DebugLogService.Info($"Mascot animation failed: {exception.Message}"); }
    }

    private void SetStatus(string message)
    {
        if (Dispatcher.UIThread.CheckAccess()) this.FindControl<TextBlock>("StatusText")!.Text = message;
        else Dispatcher.UIThread.Post(() => this.FindControl<TextBlock>("StatusText")!.Text = message);
    }
}
