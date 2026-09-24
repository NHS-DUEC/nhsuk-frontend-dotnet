using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NhsukFrontend.Parity;

/// <summary>
/// Turns HTML into a canonical list of tokens so upstream and .NET output can be compared
/// without caring about things browsers ignore:
///   - indentation and whitespace between tags; runs of whitespace in text
///   - attribute order, quote style, and entity encoding (&amp;#39; vs &amp;#x27;, © vs &amp;#xA9;)
///   - <c>disabled</c> vs <c>disabled=""</c>, <c>&lt;input&gt;</c> vs <c>&lt;input /&gt;</c>,
///     and <c>&lt;path /&gt;</c> vs <c>&lt;path&gt;&lt;/path&gt;</c>
/// Everything else (element names, nesting, attribute values, class order, text) must match.
/// </summary>
public static partial class HtmlNormalizer
{
    public static IReadOnlyList<string> Tokens(string html)
    {
        var tokens = new List<string>();
        var text = new StringBuilder();
        var i = 0;

        void FlushText()
        {
            var t = Collapse(WebUtility.HtmlDecode(text.ToString()));
            if (t.Length > 0) tokens.Add(Escape(t));
            text.Clear();
        }

        while (i < html.Length)
        {
            var c = html[i];
            if (c == '<' && i + 1 < html.Length)
            {
                var next = html[i + 1];
                if (html.AsSpan(i).StartsWith("<!--"))
                {
                    FlushText();
                    var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    i = end < 0 ? html.Length : end + 3;
                    continue;
                }
                if (next == '!')
                {
                    FlushText();
                    var end = html.IndexOf('>', i);
                    tokens.Add(html[i..(end + 1)].ToLowerInvariant());
                    i = end + 1;
                    continue;
                }
                if (char.IsLetter(next) || next == '/')
                {
                    FlushText();
                    i = ReadTag(html, i, tokens);
                    continue;
                }
            }
            text.Append(c);
            i++;
        }
        FlushText();
        return tokens;
    }

    private static int ReadTag(string html, int start, List<string> tokens)
    {
        var i = start + 1;
        var closing = html[i] == '/';
        if (closing) i++;

        var nameStart = i;
        while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] != '>' && html[i] != '/') i++;
        var name = html[nameStart..i].ToLowerInvariant();

        var attributes = new List<(string Name, string Value)>();
        var selfClosing = false;
        while (i < html.Length)
        {
            while (i < html.Length && (char.IsWhiteSpace(html[i]) || html[i] == '/'))
            {
                selfClosing = html[i] == '/';
                i++;
            }
            if (i >= html.Length || html[i] == '>') { i++; break; }
            selfClosing = false;

            var attrStart = i;
            while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] != '=' && html[i] != '>' && html[i] != '/') i++;
            var attrName = html[attrStart..i].ToLowerInvariant();
            var value = "";

            while (i < html.Length && char.IsWhiteSpace(html[i])) i++;
            if (i < html.Length && html[i] == '=')
            {
                i++;
                while (i < html.Length && char.IsWhiteSpace(html[i])) i++;
                if (html[i] is '"' or '\'')
                {
                    var quote = html[i++];
                    var end = html.IndexOf(quote, i);
                    value = html[i..end];
                    i = end + 1;
                }
                else
                {
                    var vStart = i;
                    while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] != '>') i++;
                    value = html[vStart..i];
                }
            }
            value = WebUtility.HtmlDecode(value);
            if (attrName == "class") value = Collapse(value);
            attributes.Add((attrName, value));
        }

        if (closing)
        {
            tokens.Add($"</{name}>");
            return i;
        }

        var sb = new StringBuilder("<").Append(name);
        foreach (var (attrName, value) in attributes.OrderBy(a => a.Name, StringComparer.Ordinal))
        {
            sb.Append(' ').Append(attrName).Append("=\"").Append(Escape(value)).Append('"');
        }
        tokens.Add(sb.Append('>').ToString());

        // `<path />` in SVG is the same as `<path></path>`. Void elements (input, img…) never get an end token.
        if (selfClosing && !VoidElements.Contains(name)) tokens.Add($"</{name}>");
        return i;
    }

    private static readonly HashSet<string> VoidElements =
        ["area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr"];

    private static string Collapse(string s) => Whitespace().Replace(s, " ").Trim();

    private static string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

public enum DiffKind { Same, Expected, Actual }

public sealed record DiffLine(DiffKind Kind, string Token);

public static class TokenDiff
{
    /// <summary>LCS diff: Expected = only upstream has it, Actual = only .NET has it.</summary>
    public static IReadOnlyList<DiffLine> Compute(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        int n = expected.Count, m = actual.Count;
        var lcs = new int[n + 1, m + 1];
        for (var a = n - 1; a >= 0; a--)
            for (var b = m - 1; b >= 0; b--)
                lcs[a, b] = expected[a] == actual[b] ? lcs[a + 1, b + 1] + 1 : Math.Max(lcs[a + 1, b], lcs[a, b + 1]);

        var result = new List<DiffLine>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (expected[x] == actual[y]) { result.Add(new(DiffKind.Same, expected[x])); x++; y++; }
            else if (lcs[x + 1, y] >= lcs[x, y + 1]) result.Add(new(DiffKind.Expected, expected[x++]));
            else result.Add(new(DiffKind.Actual, actual[y++]));
        }
        while (x < n) result.Add(new(DiffKind.Expected, expected[x++]));
        while (y < m) result.Add(new(DiffKind.Actual, actual[y++]));
        return result;
    }
}
