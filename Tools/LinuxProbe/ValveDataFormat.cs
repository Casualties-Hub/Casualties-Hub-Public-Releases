namespace CasualtiesHub.LinuxProbe;

/// <summary>
/// Minimal reader for Valve's KeyValues text format (libraryfolders.vdf, appmanifest_*.acf).
///
/// This is a prototype for the parser that will live in Casualties Hub.Core. It is a real
/// tokeniser rather than a regex because the format has two incompatible shapes in the wild
/// (see <see cref="VdfNode.CollectLibraryPaths"/>) and because values carry C-style escapes,
/// which a regex cannot unescape correctly.
/// </summary>
public sealed class VdfNode
{
    public string Name { get; init; } = "";

    /// <summary>Set on leaves. Null on objects.</summary>
    public string? Value { get; init; }

    public List<VdfNode> Children { get; } = [];

    public bool IsObject => Value is null;

    /// <summary>First child matching <paramref name="name"/>, case-insensitively. Valve is inconsistent about casing.</summary>
    public VdfNode? Child(string name) =>
        Children.FirstOrDefault(child => child.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public string? ChildValue(string name) => Child(name)?.Value;

    /// <summary>
    /// Every Steam library path in a libraryfolders.vdf, covering both known layouts:
    ///   modern  "libraryfolders" { "0" { "path" "/home/u/.local/share/Steam"  "apps" { ... } } }
    ///   legacy  "LibraryFolders" { "1" "D:\\SteamLibrary"  "TimeNextStatsReport" "..." }
    /// The legacy shape stores the path as a bare value on a numeric key, so non-numeric
    /// bookkeeping keys (TimeNextStatsReport, ContentStatsID) must be filtered out.
    /// </summary>
    public IEnumerable<string> CollectLibraryPaths()
    {
        foreach (var child in Children)
        {
            if (child.IsObject)
            {
                if (child.ChildValue("path") is { Length: > 0 } path) yield return path;
            }
            else if (int.TryParse(child.Name, out _) && child.Value is { Length: > 0 } legacyPath)
            {
                yield return legacyPath;
            }
        }
    }

    public static VdfNode Parse(string text)
    {
        var index = 0;
        var root = new VdfNode { Name = "<root>" };
        ParseInto(root, text, ref index, depth: 0);
        return root;
    }

    private static void ParseInto(VdfNode parent, string text, ref int index, int depth)
    {
        // Guards against a malformed file driving unbounded recursion. Real files nest ~4 deep.
        if (depth > 32) return;

        while (true)
        {
            var key = ReadToken(text, ref index);
            if (key is null || key == "}") return;
            if (key == "{") continue; // stray brace; skip rather than abort the whole file

            var save = index;
            var next = ReadToken(text, ref index);
            switch (next)
            {
                case null:
                    return;
                case "{":
                {
                    var node = new VdfNode { Name = key };
                    ParseInto(node, text, ref index, depth + 1);
                    parent.Children.Add(node);
                    break;
                }
                case "}":
                    // Key with no value at the end of a block. Keep the key, restore the brace.
                    parent.Children.Add(new VdfNode { Name = key, Value = "" });
                    index = save;
                    return;
                default:
                    parent.Children.Add(new VdfNode { Name = key, Value = next });
                    break;
            }
        }
    }

    /// <summary>Returns the next token, "{", "}", or null at end of input.</summary>
    private static string? ReadToken(string text, ref int index)
    {
        while (true)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            if (index >= text.Length) return null;

            // Line comments. Valve writes these into some config files.
            if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] is not ('\n' or '\r')) index++;
                continue;
            }
            break;
        }

        var c = text[index];
        if (c is '{' or '}')
        {
            index++;
            return c.ToString();
        }

        if (c == '"')
        {
            index++;
            var value = new System.Text.StringBuilder();
            while (index < text.Length && text[index] != '"')
            {
                if (text[index] == '\\' && index + 1 < text.Length)
                {
                    index++;
                    value.Append(text[index] switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        var escaped => escaped // covers \\ and \"
                    });
                }
                else
                {
                    value.Append(text[index]);
                }
                index++;
            }
            index++; // closing quote
            return value.ToString();
        }

        // Unquoted token. Rare, but appears in hand-edited files.
        var start = index;
        while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] is not ('{' or '}' or '"')) index++;
        return index > start ? text[start..index] : null;
    }
}
