using System.Diagnostics;
using System.IO;

namespace Casualties_Hub.Services;

/// <summary>
/// Opening links, folders and files on the desktop.
/// </summary>
/// <remarks>
/// Hub content is fetched from GitHub and treated as untrusted input, yet its URLs reach
/// Process.Start. Both xdg-open and ShellExecute will act on file:// or a registered custom
/// scheme, so without the allow-list a hostile feed could open something unintended.
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
    /// Shows a file to the user by opening its containing folder. There is no portable way to
    /// select the file itself, so this does not pretend otherwise.
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
                // Safe because callers pass the scheme allow-list above or an existence check,
                // never raw input.
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
            DebugLogService.Error($"Could not open {description}.", exception);
        }
    }
}
