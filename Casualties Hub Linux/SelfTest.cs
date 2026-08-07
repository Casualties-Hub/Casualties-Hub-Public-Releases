using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Casualties_Hub.Models;
using Casualties_Hub.Services;
using Casualties_Hub.Views;

namespace Casualties_Hub;

/// <summary>
/// Constructs every page once and reports which ones survived.
/// </summary>
/// <remarks>
/// Pages read settings, walk the filesystem and build controls in their constructors, so a bad
/// binding or a null reference there throws only when the user clicks that nav button. Launching
/// the app proves nothing about pages the launch never opens. This exercises all of them, and
/// runs headless so it works over SSH and in CI as well as on a desktop.
/// </remarks>
public static class SelfTest
{
    public static int Run(AppBuilder builder)
    {
        var failures = 0;

        // Avalonia refuses to build controls before a platform is initialised. The headless
        // platform gives a real control tree with no display attached.
        // UseHeadlessDrawing = false keeps real Skia rendering, so the skin preview below can be
        // rasterised to a PNG. The stub renderer would produce an empty image.
        builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        foreach (var (name, factory) in new (string, Func<UserControl>)[]
                 {
                     ("HubHomePage", () => new HubHomePage(_ => { })),
                     ("DashboardPage", () => new DashboardPage(_ => { })),
                     ("ModsPage", () => new ModsPage(_ => { })),
                     ("SkinsAndBackupsPage", () => new SkinsAndBackupsPage(_ => { })),
                     ("SkinsPage", () => new SkinsPage(_ => { })),
                     ("ProtectedFilesPage", () => new ProtectedFilesPage(_ => { })),
                     ("BackupsPage", () => new BackupsPage(_ => { })),
                     ("MultiplayerPage", () => new MultiplayerPage(_ => { })),
                     ("SettingsPage", () => new SettingsPage(_ => { })),
                     ("CreditsPage", () => new CreditsPage()),
                 })
        {
            try
            {
                var page = factory();
                Console.WriteLine($"  OK      {name}  ({page.GetType().Name} constructed)");
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine($"  FAILED  {name}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        foreach (var (name, factory) in new (string, Func<Window>)[]
                 {
                     ("HubDialog", () => new HubDialog()),
                     ("SkinSlotDialog", () => new SkinSlotDialog()),
                     ("UninstallDialog", () => new UninstallDialog()),
                 })
        {
            try
            {
                _ = factory();
                Console.WriteLine($"  OK      {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine($"  FAILED  {name}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        failures += CheckSkinPreview();

        Console.WriteLine(failures == 0
            ? "RESULT: all checks passed."
            : $"RESULT: {failures} check(s) failed.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Composes a real skin preview if the machine has one installed.
    /// </summary>
    /// <remarks>
    /// The renderer decodes PNGs and builds a transformed control tree, none of which a
    /// constructor check touches. Running it against actual sprites is the only way to confirm
    /// the Avalonia port draws anything at all without looking at a screen.
    /// </remarks>
    private static int CheckSkinPreview()
    {
        try
        {
            var slots = new SkinLibraryService(new SettingsService(), new ModService()).DiscoverSlots();
            if (slots.Count == 0)
            {
                Console.WriteLine("  SKIP    skin preview (no CustomSprites slots installed)");
                return 0;
            }

            var slot = slots[0];
            var canvas = SkinPreviewComposer.Compose(
                slot.FolderPath, SkinHeadShape.Normal, SkinEyeExpression.Open, facingBack: false);

            if (canvas.Children.Count == 0)
            {
                Console.WriteLine($"  FAILED  skin preview: {slot.Name} composed 0 sprites");
                return 1;
            }

            Console.WriteLine($"  OK      skin preview: {slot.Name} composed {canvas.Children.Count} sprite(s) "
                              + $"onto a {canvas.Width:F0}x{canvas.Height:F0} canvas");

            // Mirroring must not change how much is drawn, only where.
            var mirrored = SkinPreviewComposer.Compose(
                slot.FolderPath, SkinHeadShape.Normal, SkinEyeExpression.Open, facingBack: true);
            if (mirrored.Children.Count != canvas.Children.Count)
            {
                Console.WriteLine($"  FAILED  skin preview: mirrored pose drew {mirrored.Children.Count}, expected {canvas.Children.Count}");
                return 1;
            }

            Console.WriteLine("  OK      skin preview: mirrored pose matches");
            SavePreviewImage(canvas, slot.Name);
            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"  FAILED  skin preview: {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Rasterises the composed preview so it can be inspected without a screen. "The preview looks
    /// wrong" is not actionable from a log line; an image is.
    /// </summary>
    private static void SavePreviewImage(Canvas canvas, string slotName)
    {
        try
        {
            // Scale up, because the rig is roughly 45px tall natively and a 1:1 render is too
            // small to judge. Nearest-neighbour keeps the pixel art crisp.
            const int scale = 8;
            var width = (int)Math.Ceiling(canvas.Width);
            var height = (int)Math.Ceiling(canvas.Height);
            if (width <= 0 || height <= 0) return;

            var host = new Border
            {
                Width = width * scale,
                Height = height * scale,
                Background = Brushes.Magenta, // Obvious backdrop, so gaps in the art are visible.
                Child = new Viewbox { Stretch = Stretch.Uniform, Child = canvas },
            };
            RenderOptions.SetBitmapInterpolationMode(host, BitmapInterpolationMode.None);

            host.Measure(new Size(host.Width, host.Height));
            host.Arrange(new Rect(0, 0, host.Width, host.Height));

            var target = new RenderTargetBitmap(new PixelSize(width * scale, height * scale));
            target.Render(host);

            var path = Path.Combine(LinuxPaths.AppDataRoot(), $"skin-preview-{slotName}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            target.Save(stream);

            Console.WriteLine($"  OK      skin preview: rendered to {path}");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"  NOTE    skin preview could not be rasterised: {exception.GetType().Name}: {exception.Message}");
        }
    }
}
