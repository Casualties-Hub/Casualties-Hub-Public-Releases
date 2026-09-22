using System.Net;
using System.Text.RegularExpressions;

namespace Casualties_Hub.Services;

/// <summary>Conservative display parser for Nexus-flavoured BBCode descriptions.</summary>
public static class NexusBbCodeParser
{
    public static string ToDisplayText(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return "No description was supplied by the mod author.";

        // Nexus writes each break as a source newline followed by a break tag. Both mean the same
        // single break, so any whitespace hugging the tag folds into it, or every line would be
        // followed by a blank one.
        var value = Regex.Replace(description, @"[ \t]*\r?\n?[ \t]*(?:<br\s*/?>|\[br\s*/?\])[ \t]*\r?\n?", "\n", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\[url=(?<url>[^\]]+)\](?<text>.*?)\[/url\]", "${text}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        value = Regex.Replace(value, @"\[url\](?<url>.*?)\[/url\]", "${url}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        value = Regex.Replace(value, @"\[/?(b|i|u|s|center|left|right|quote|code|list|\*|color(?:=[^\]]+)?|size(?:=[^\]]+)?)\]", "", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\[[^\]]+\]", "");
        value = WebUtility.HtmlDecode(value);
        value = Regex.Replace(value, @"\n{3,}", "\n\n");
        return value.Trim();
    }
}
