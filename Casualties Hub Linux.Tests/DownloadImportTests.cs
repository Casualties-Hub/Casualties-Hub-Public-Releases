using Casualties_Hub.Services;
using Xunit;

namespace Casualties_Hub.Tests;

/// <summary>
/// Detecting when a download has actually finished.
/// </summary>
/// <remarks>
/// This is the Linux bug with the least visible symptom. The Windows check opens the file with
/// FileShare.None and treats success as "finished", which is sound on Windows because the writer
/// holds a mandatory lock. Linux locking is advisory, so that open succeeds while a browser is
/// still writing, and the Hub would extract a truncated archive into somebody's game folder.
/// </remarks>
public sealed class DownloadImportTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("chdownload").FullName;

    // Short intervals so the suite stays fast; the shipped values are 1s x 3 reads.
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(20);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private string Path(string name) => System.IO.Path.Combine(_root, name);

    [Fact]
    public async Task Reports_finished_once_the_size_stops_changing()
    {
        var file = Path("mod.zip");
        await File.WriteAllBytesAsync(file, new byte[1024]);

        Assert.True(await DownloadImportService.WaitForStableSizeAsync(file, Tick, 3, 50));
    }

    [Fact]
    public async Task Waits_while_the_file_is_still_growing()
    {
        var file = Path("growing.zip");
        await File.WriteAllBytesAsync(file, new byte[16]);

        // Mimic a download in flight: keep appending while the check runs.
        var writing = true;
        var writer = Task.Run(async () =>
        {
            for (var i = 0; i < 12 && writing; i++)
            {
                await using (var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    await stream.WriteAsync(new byte[512]);
                await Task.Delay(Tick);
            }
        });

        // Too few attempts to outlast the writer, so this must NOT report finished.
        var finished = await DownloadImportService.WaitForStableSizeAsync(file, Tick, 3, 6);
        writing = false;
        await writer;

        Assert.False(finished);
    }

    [Fact]
    public async Task Reports_finished_after_a_growing_file_settles()
    {
        var file = Path("settles.zip");
        await File.WriteAllBytesAsync(file, new byte[16]);

        var writer = Task.Run(async () =>
        {
            for (var i = 0; i < 3; i++)
            {
                await using (var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    await stream.WriteAsync(new byte[256]);
                await Task.Delay(Tick);
            }
        });

        var finished = await DownloadImportService.WaitForStableSizeAsync(file, Tick, 3, 200);
        await writer;

        Assert.True(finished);
    }

    [Fact]
    public async Task An_empty_file_is_never_treated_as_finished()
    {
        // Browsers create the target file at zero bytes before any data arrives.
        var file = Path("empty.zip");
        await File.WriteAllBytesAsync(file, []);

        Assert.False(await DownloadImportService.WaitForStableSizeAsync(file, Tick, 3, 8));
    }

    [Fact]
    public async Task A_cancelled_download_stops_immediately()
    {
        var file = Path("gone.zip");
        await File.WriteAllBytesAsync(file, new byte[64]);
        File.Delete(file);

        Assert.False(await DownloadImportService.WaitForStableSizeAsync(file, Tick, 3, 50));
    }

    [Theory]
    [InlineData("mod.zip.part", true)]
    [InlineData("mod.zip.crdownload", true)]
    [InlineData("mod.zip.download", true)]
    [InlineData("mod.zip.tmp", true)]
    [InlineData("mod.zip.opdownload", true)]
    [InlineData(".hidden.zip", true)]
    [InlineData("mod.zip", false)]
    [InlineData("mod.7z", false)]
    [InlineData("Mod.RAR", false)]
    public void Partial_download_names_are_recognised(string name, bool expected)
    {
        // Firefox writes .part, Chrome .crdownload. Inspecting those wastes time and logs noise.
        Assert.Equal(expected, DownloadImportService.IsPartialDownload(name));
    }

    [Theory]
    [InlineData("mod.zip", true)]
    [InlineData("mod.7z", true)]
    [InlineData("mod.rar", true)]
    [InlineData("mod.ZIP", true)]
    [InlineData("mod.dll", false)]
    [InlineData("readme.txt", false)]
    public void Supported_archives_are_matched_regardless_of_extension_case(string name, bool expected)
    {
        Assert.Equal(expected, ModService.IsSupportedArchive(name));
    }
}
