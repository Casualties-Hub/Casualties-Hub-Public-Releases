using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Casualties_Hub.Views;

/// <summary>
/// The single "Skins &amp; Backups" sidebar entry.
/// </summary>
/// <remarks>
/// Protected files, the skin preview and plugin-folder backups, switched between rather than
/// tiled: the preview needs the width. Sections are the existing pages, so each stays
/// independently testable.
/// </remarks>
public partial class SkinsAndBackupsPage : UserControl
{
    private readonly Action<string> _setStatus;
    private Button? _activeTab;

    public SkinsAndBackupsPage() : this(_ => { }) { }

    public SkinsAndBackupsPage(Action<string> setStatus)
    {
        _setStatus = setStatus;
        AvaloniaXamlLoader.Load(this);

        var skinsTab = this.FindControl<Button>("SkinsTab")!;
        var protectedTab = this.FindControl<Button>("ProtectedTab")!;
        var backupsTab = this.FindControl<Button>("BackupsTab")!;

        skinsTab.Click += (_, _) => Show(() => new SkinsPage(_setStatus), skinsTab);
        protectedTab.Click += (_, _) => Show(() => new ProtectedFilesPage(_setStatus), protectedTab);
        backupsTab.Click += (_, _) => Show(() => new BackupsPage(_setStatus), backupsTab);

        Show(() => new SkinsPage(_setStatus), skinsTab);
    }

    private void Show(Func<UserControl> buildSection, Button tab)
    {
        if (_activeTab is not null) _setStatus(MainWindow.IdleStatus);

        this.FindControl<ContentControl>("SectionHost")!.Content = buildSection();

        // Style classes rather than triggers, the same approach the sidebar uses.
        _activeTab?.Classes.Remove("accent");
        tab.Classes.Add("accent");
        _activeTab = tab;
    }
}
