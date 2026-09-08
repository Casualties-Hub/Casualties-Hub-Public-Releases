using Casualties_Hub.Services;
using Xunit;

namespace Casualties_Hub.Tests;

/// <summary>
/// Steam's KeyValues parser. If this is wrong the Hub finds no libraries and reports the game as
/// not installed, with no error to explain why, so it is worth pinning down against the real
/// shapes Valve writes.
/// </summary>
public sealed class ValveDataFormatTests
{
    [Fact]
    public void Parses_the_modern_nested_libraryfolders()
    {
        const string vdf = """
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"/home/u/.local/share/Steam"
            		"label"		""
            		"apps"
            		{
            			"228980"		"434588"
            		}
            	}
            	"1"
            	{
            		"path"		"/mnt/games/SteamLibrary"
            		"label"		"games"
            	}
            }
            """;

        var paths = VdfNode.Parse(vdf).Children.First().CollectLibraryPaths().ToList();

        Assert.Equal(["/home/u/.local/share/Steam", "/mnt/games/SteamLibrary"], paths);
    }

    [Fact]
    public void Parses_the_legacy_flat_libraryfolders()
    {
        // Older clients wrote the path as a bare value on a numeric key, mixed in with
        // bookkeeping entries that must not be mistaken for libraries.
        const string vdf = """
            "LibraryFolders"
            {
            	"TimeNextStatsReport"		"1234567890"
            	"ContentStatsID"		"-987654321"
            	"1"		"D:\\SteamLibrary"
            	"2"		"E:\\Games\\Steam"
            }
            """;

        var paths = VdfNode.Parse(vdf).Children.First().CollectLibraryPaths().ToList();

        Assert.Equal([@"D:\SteamLibrary", @"E:\Games\Steam"], paths);
    }

    [Fact]
    public void Unescapes_backslashes_and_quotes()
    {
        const string vdf = """
            "root"
            {
            	"path"		"C:\\Program Files\\Steam"
            	"quoted"		"a \"b\" c"
            }
            """;

        var root = VdfNode.Parse(vdf).Children.First();

        Assert.Equal(@"C:\Program Files\Steam", root.ChildValue("path"));
        Assert.Equal("a \"b\" c", root.ChildValue("quoted"));
    }

    [Fact]
    public void Reads_an_app_manifest()
    {
        const string acf = """
            "AppState"
            {
            	"appid"		"3167550"
            	"name"		"Casualties Unknown Demo"
            	"installdir"		"Casualties Unknown Demo"
            	"StateFlags"		"4"
            }
            """;

        var state = VdfNode.Parse(acf).Children.First();

        Assert.Equal("3167550", state.ChildValue("appid"));
        Assert.Equal("Casualties Unknown Demo", state.ChildValue("installdir"));
    }

    [Fact]
    public void Child_lookup_ignores_key_casing()
    {
        // Valve is inconsistent between "AppID" and "appid" across client versions.
        var state = VdfNode.Parse("\"AppState\"\n{\n\t\"AppID\"\t\"123\"\n}").Children.First();

        Assert.Equal("123", state.ChildValue("appid"));
    }

    [Fact]
    public void Skips_line_comments()
    {
        const string vdf = """
            "root"
            {
            	// a comment Valve sometimes writes
            	"path"		"/games"
            }
            """;

        Assert.Equal("/games", VdfNode.Parse(vdf).Children.First().ChildValue("path"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"unclosed\" {")]
    [InlineData("}}}}")]
    [InlineData("\"a\" \"b\" \"c\"")]
    public void Malformed_input_does_not_throw(string text)
    {
        // A truncated or corrupt VDF must degrade to "no libraries found", never crash startup.
        var exception = Record.Exception(() => VdfNode.Parse(text).CollectLibraryPaths().ToList());

        Assert.Null(exception);
    }
}
