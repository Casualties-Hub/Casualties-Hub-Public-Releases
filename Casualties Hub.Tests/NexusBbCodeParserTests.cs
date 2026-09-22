using Casualties_Hub.Services;
using Xunit;

namespace Casualties_Hub.Tests;

/// <summary>
/// Nexus descriptions arrive as BBCode with HTML breaks. The parser only has to produce readable
/// plain text, but line spacing is the part users notice immediately.
/// </summary>
public sealed class NexusBbCodeParserTests
{
    [Fact]
    public void SourceNewlineAndBreakTagCollapseToOneBreak()
    {
        // This is the exact shape Nexus emits: a newline for the source, then the real break.
        var text = NexusBbCodeParser.ToDisplayText("Line one\n<br />Line two\n<br />Line three");

        Assert.Equal("Line one\nLine two\nLine three", text);
    }

    [Fact]
    public void TwoBreakTagsKeepAParagraphGap()
    {
        var text = NexusBbCodeParser.ToDisplayText("Heading\n<br />\n<br />Body");

        Assert.Equal("Heading\n\nBody", text);
    }

    [Fact]
    public void BbCodeBreakAndBareBreakTagsWork()
    {
        Assert.Equal("a\nb", NexusBbCodeParser.ToDisplayText("a[br]b"));
        Assert.Equal("a\nb", NexusBbCodeParser.ToDisplayText("a<br>b"));
        Assert.Equal("a\nb", NexusBbCodeParser.ToDisplayText("a <BR/> b"));
    }

    [Fact]
    public void FormattingTagsAreStrippedAndLinksKeepTheirText()
    {
        var text = NexusBbCodeParser.ToDisplayText("[size=4][b]Bold[/b][/size] [url=https://example.test]site[/url]");

        Assert.Equal("Bold site", text);
    }
}
