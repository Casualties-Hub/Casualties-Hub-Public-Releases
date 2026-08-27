using Casualties_Hub.Models;
using Casualties_Hub.Services;
using Xunit;

namespace Casualties_Hub.Tests;

/// <summary>
/// The uninstall helper script.
/// </summary>
/// <remarks>
/// This is the highest-consequence code in the Linux Edition. Paths reach it from
/// ProtectedFilesPath and BackupPath, which live in a JSON file the user can edit by hand, and
/// they are interpolated into a script that runs rm -rf. Getting the quoting or the containment
/// check wrong deletes somebody's home directory, so both are pinned down here rather than
/// checked by running the thing.
/// </remarks>
public sealed class UninstallScriptTests
{
    private const string Staging = "/tmp/CasualtiesHub/Uninstall/abc123";

    private static string ScriptFor(string path) =>
        UninstallService.BuildScript([path], processId: 1234, Staging);

    // --- quoting -----------------------------------------------------------

    [Fact]
    public void An_embedded_single_quote_is_escaped()
    {
        // The classic break: a naive wrapper would end the quoted string here and let the rest
        // of the path run as shell code.
        var quoted = UninstallService.ShellQuote("/home/zach/it's mine");

        Assert.Equal(@"'/home/zach/it'\''s mine'", quoted);
    }

    [Theory]
    [InlineData("/tmp/$HOME")]
    [InlineData("/tmp/$(rm -rf ~)")]
    [InlineData("/tmp/`whoami`")]
    [InlineData("/tmp/*")]
    [InlineData("/tmp/a;rm -rf /")]
    [InlineData("/tmp/a|tee /etc/passwd")]
    [InlineData("/tmp/a\nrm -rf /")]
    public void Shell_metacharacters_stay_inert(string path)
    {
        // Single quotes suppress every expansion, so each of these must survive verbatim
        // inside the quotes rather than being evaluated.
        var quoted = UninstallService.ShellQuote(path);

        Assert.StartsWith("'", quoted);
        Assert.EndsWith("'", quoted);
        Assert.Contains(path, quoted);
        // No apostrophes in these inputs, so exactly two quote characters should be present.
        Assert.Equal(2, quoted.Count(c => c == '\''));
    }

    [Fact]
    public void The_script_waits_for_the_hub_to_exit_before_deleting()
    {
        var script = ScriptFor("/tmp/CasualtiesHub/x");

        Assert.Contains("PID=1234", script);
        Assert.Contains("kill -0", script);
        // The wait must be bounded, or a wedged Hub leaves this spinning forever.
        Assert.Contains("-lt 60", script);
    }

    [Fact]
    public void The_script_removes_its_own_staging_directory_by_absolute_path()
    {
        var script = ScriptFor("/tmp/CasualtiesHub/x");

        // Path.GetFullPath is platform-dependent, and this suite runs on Windows where a
        // driveless "/tmp/..." resolves against the current drive. Compare against the same
        // normalisation the code performs rather than the literal input.
        var expected = UninstallService.ShellQuote(
            Path.GetFullPath(Staging).TrimEnd(Path.DirectorySeparatorChar));

        Assert.Contains($"STAGING={expected}", script);
        Assert.Contains("rm -rf -- \"$STAGING\"", script);
    }

    [Fact]
    public void The_self_delete_never_derives_its_target_from_the_scripts_location()
    {
        // Regression cover for a real one. The script used rm -rf -- "$(dirname "$0")", which
        // targets wherever the script is being run from, not where it was created. Executing a
        // copy placed in /tmp therefore issued rm -rf /tmp. Verified by running the generated
        // script under /bin/sh, which destroyed every unrelated file in the sandbox.
        var script = ScriptFor("/tmp/CasualtiesHub/x");

        Assert.DoesNotContain("dirname", script);
        Assert.DoesNotContain("$0", script);
    }

    [Fact]
    public void An_unexpected_staging_path_is_not_deleted()
    {
        // If the staging path does not carry the marker, the guard skips it rather than
        // recursing into whatever it happens to point at.
        var script = UninstallService.BuildScript(["/tmp/CasualtiesHub/x"], 1234, "/tmp");

        // The guard is what makes an unexpected value harmless: no marker, no delete.
        Assert.Contains($"*{UninstallService.StagingMarker}*", script);
        Assert.Contains("case \"$STAGING\" in", script);
        Assert.DoesNotContain(UninstallService.StagingMarker, UninstallService.ShellQuote(Path.GetFullPath("/tmp")));
    }

    [Fact]
    public void The_script_uses_a_posix_shebang_not_bash()
    {
        // /bin/sh is present everywhere; bash is not guaranteed on minimal images.
        Assert.StartsWith("#!/bin/sh", ScriptFor("/tmp/CasualtiesHub/x"));
    }

    [Fact]
    public void Rm_uses_a_double_dash_so_a_leading_dash_is_not_read_as_a_flag()
    {
        Assert.Contains("rm -rf --", ScriptFor("/tmp/CasualtiesHub/x"));
    }

    // --- containment -------------------------------------------------------

    [Theory]
    [InlineData("/")]
    [InlineData("/home")]
    [InlineData("/usr")]
    [InlineData("/etc")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    [InlineData("   ")]
    public void Dangerous_targets_are_refused(string path)
    {
        Assert.False(UninstallService.IsSafeToDelete(path));
    }

    [Fact]
    public void The_home_directory_itself_is_refused()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.False(UninstallService.IsSafeToDelete(home));
        Assert.False(UninstallService.IsSafeToDelete(home + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void A_path_outside_every_owned_root_is_refused()
    {
        // The failure mode this guards: someone edits ProtectedFilesPath to point at their
        // documents, and the uninstaller happily recurses through it.
        var documents = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Important");

        Assert.False(UninstallService.IsSafeToDelete(documents));
    }

    [Fact]
    public void Traversal_out_of_an_owned_root_is_refused()
    {
        var escaped = Path.Combine(LinuxPaths.AppDataRoot(), "..", "..", "..", "etc");

        Assert.False(UninstallService.IsSafeToDelete(escaped));
    }

    [Fact]
    public void The_hub_data_directory_is_allowed()
    {
        Assert.True(UninstallService.IsSafeToDelete(Path.Combine(LinuxPaths.AppDataRoot(), "Settings.json")));
        Assert.True(UninstallService.IsSafeToDelete(Path.Combine(LinuxPaths.AppDataRoot(), "Logs")));
    }

    [Fact]
    public void The_temp_staging_area_is_allowed()
    {
        Assert.True(UninstallService.IsSafeToDelete(Path.Combine(Path.GetTempPath(), "CasualtiesHub", "Uninstall")));
    }

    [Fact]
    public void Unsafe_paths_are_dropped_before_the_script_is_built()
    {
        var items = new List<UninstallItem>
        {
            new() { Key = "Bad", Title = "Bad", Description = "test", Paths = ["/", "/etc", Path.Combine(LinuxPaths.AppDataRoot(), "Logs")] },
        };

        var resolved = UninstallService.ResolveDeletablePaths(items);

        Assert.Single(resolved);
        Assert.EndsWith("Logs", resolved[0]);
    }

    [Fact]
    public void Emits_a_sample_script_for_execution_against_a_real_shell()
    {
        // Asserting on the script text proves the quoting is shaped right, not that a shell
        // agrees. This writes a script targeting a known sandbox layout so it can be run under
        // /bin/sh and checked: the awkward names must be deleted and their neighbours must not.
        var sandbox = "/tmp/ch-uninstall-sandbox";
        string[] targets =
        [
            $"{sandbox}/plain",
            $"{sandbox}/with space",
            $"{sandbox}/it's quoted",
            $"{sandbox}/dollar $HOME sign",
            $"{sandbox}/back`tick`",
            $"{sandbox}/semi;colon",
            $"{sandbox}/star*glob",
        ];

        var script = UninstallService.BuildScript(targets, processId: 1, $"{sandbox}/staging{UninstallService.StagingMarker}run");
        var outputPath = Path.Combine(Path.GetTempPath(), "ch-uninstall-sample.sh");
        File.WriteAllText(outputPath, script.Replace("\r\n", "\n"));

        Assert.True(File.Exists(outputPath));
        foreach (var target in targets)
            Assert.Contains(UninstallService.ShellQuote(target), script);
    }

    [Fact]
    public void Duplicate_paths_are_collapsed()
    {
        var logs = Path.Combine(LinuxPaths.AppDataRoot(), "Logs");
        var items = new List<UninstallItem>
        {
            new() { Key = "A", Title = "A", Description = "test", Paths = [logs] },
            new() { Key = "B", Title = "B", Description = "test", Paths = [logs] },
        };

        Assert.Single(UninstallService.ResolveDeletablePaths(items));
    }
}
