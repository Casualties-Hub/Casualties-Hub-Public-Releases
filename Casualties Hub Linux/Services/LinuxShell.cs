using System.Diagnostics;
using System.IO;

namespace Casualties_Hub.Services;

/// <summary>
/// Opening links, folders and files on the desktop.
/// </summary>
/// <remarks>
/// Replaces the Windows Hub's nine hardcoded <c>explorer.exe</c> and <c>notepad.exe</c> launches
/// with xdg-open, and adds a scheme allow-list. That last part is not cosmetic: Hub content is
/// fetched from GitHub and treated as untrusted input, yet its URLs reach Process.Start with
/// UseShellExecute directly. On a desktop, xdg-open will happily act on file:// or a registered
/// custom scheme, so a hostile feed could open something unintended.
/// </remarks>
public static class LinuxShell
{
    private static readonly string[] AllowedSchemes = ["http", "https", "steam"];

    /// <summary>Opens a web or steam:// link, rejecting anything else.</summary>
    public static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            DebugLogService.Info($"Refused to open a link with an unsupported scheme: {url}");
            return;
        }

        Open(uri.ToString(), $"link {uri.Scheme}://…");
    }

    /// <summary>Opens a folder in the user's file manager.</summary>
    public static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            DebugLogService.Info($"Cannot open a folder that does not exist: {path}");
            return;
        }
        Open(path, "folder");
    }

    /// <summary>
    /// Shows a file to the user. There is no portable "reveal and select" on Linux, so this opens
    /// the containing folder rather than pretending otherwise.
    /// </summary>
    public static void RevealFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var folder = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (folder is not null) OpenFolder(folder);
    }

    /// <summary>Opens a file in whatever the desktop has registered for it.</summary>
    public static void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            DebugLogService.Info($"Cannot open a file that does not exist: {path}");
            return;
        }
        Open(path, "file");
    }

    private static void Open(string target, string description)
    {
        // ArgumentList, not a quoted command string: paths here can contain spaces and quotes,
        // and this avoids constructing a shell command at all.
        try
        {
            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                // No xdg-open on Windows. Safe because callers pass the scheme allow-list above
                // or an existence check, never raw input.
                startInfo = new ProcessStartInfo(target) { UseShellExecute = true };
            }
            else
            {
                startInfo = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
                startInfo.ArgumentList.Add(target);
            }

            Process.Start(startInfo);
            DebugLogService.Activity("Shell", $"Opened {description}.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // xdg-open comes from xdg-utils, which a minimal install may not have.
            DebugLogService.Error($"Could not open {description}; is xdg-utils installed?", exception);
        }
    }
}
