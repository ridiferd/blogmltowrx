using System.Text;
using HtmlAgilityPack;

namespace BlogMlToWxr.Transform;

/// <summary>
/// Converts classic BlogEngine HTML into Gutenberg block markup.
/// Code blocks get first class treatment because BlogEngine stores them as
/// &lt;pre class="brush: csharp"&gt; or &lt;pre&gt;&lt;code class="language-csharp"&gt;.
/// </summary>
public sealed class GutenbergConverter
{
    private static readonly Dictionary<string, string> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs"] = "csharp",
        ["c#"] = "csharp",
        ["csharp"] = "csharp",
        ["js"] = "javascript",
        ["jscript"] = "javascript",
        ["javascript"] = "javascript",
        ["ts"] = "typescript",
        ["typescript"] = "typescript",
        ["xml"] = "markup",
        ["html"] = "markup",
        ["xaml"] = "markup",
        ["sql"] = "sql",
        ["ps"] = "powershell",
        ["powershell"] = "powershell",
        ["bash"] = "bash",
        ["shell"] = "bash",
        ["json"] = "json",
        ["yaml"] = "yaml",
        ["python"] = "python",
        ["py"] = "python",
        ["css"] = "css",
        ["plain"] = "plaintext",
        ["text"] = "plaintext"
    };

    public string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var document = new HtmlDocument { OptionOutputOriginalCase = true };
        document.LoadHtml(html);

        var builder = new StringBuilder();

        foreach (var node in document.DocumentNode.ChildNodes)
        {
            AppendBlock(builder, node);
        }

        return builder.ToString().Trim();
    }

    private void AppendBlock(StringBuilder builder, HtmlNode node)
    {
        switch (node.NodeType)
        {
            case HtmlNodeType.Comment:
                return;

            case HtmlNodeType.Text:
                var text = node.InnerText;
                if (string.IsNullOrWhiteSpace(text)) return;
                Append(builder, "wp:paragraph", $"<p>{text.Trim()}</p>");
                return;

            case HtmlNodeType.Element:
                AppendElement(builder, node);
                return;
        }
    }

    private void AppendElement(StringBuilder builder, HtmlNode node)
    {
        var name = node.Name.ToLowerInvariant();

        switch (name)
        {
            case "pre":
                AppendCode(builder, node);
                return;

            case "p":
                if (IsImageOnly(node, out var innerImage))
                {
                    AppendImage(builder, innerImage!);
                    return;
                }
                if (string.IsNullOrWhiteSpace(node.InnerText) && node.SelectSingleNode(".//img") is null) return;
                Append(builder, "wp:paragraph", node.OuterHtml.Trim());
                return;

            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                var level = int.Parse(name[1..]);
                var attributes = level == 2 ? string.Empty : $" {{\"level\":{level}}}";
                Append(builder, "wp:heading" + attributes, node.OuterHtml.Trim());
                return;

            case "ul":
                Append(builder, "wp:list", WrapListItems(node));
                return;

            case "ol":
                Append(builder, "wp:list {\"ordered\":true}", WrapListItems(node));
                return;

            case "blockquote":
                Append(builder, "wp:quote", node.OuterHtml.Trim());
                return;

            case "table":
                Append(builder, "wp:table", $"<figure class=\"wp-block-table\">{node.OuterHtml.Trim()}</figure>");
                return;

            case "img":
                AppendImage(builder, node);
                return;

            case "figure":
                Append(builder, "wp:image", node.OuterHtml.Trim());
                return;

            case "hr":
                Append(builder, "wp:separator", "<hr class=\"wp-block-separator\"/>");
                return;

            case "div":
            case "section":
            case "article":
                // Unwrap plain containers so the inner content becomes real blocks.
                if (node.Attributes.Count == 0)
                {
                    foreach (var child in node.ChildNodes) AppendBlock(builder, child);
                    return;
                }
                Append(builder, "wp:html", node.OuterHtml.Trim());
                return;

            case "script":
            case "style":
                return;

            default:
                if (string.IsNullOrWhiteSpace(node.OuterHtml)) return;
                Append(builder, "wp:html", node.OuterHtml.Trim());
                return;
        }
    }

    private static string WrapListItems(HtmlNode node)
    {
        var inner = node.OuterHtml.Trim();
        var items = node.SelectNodes("./li");
        if (items is null) return inner;

        var rebuilt = new StringBuilder();
        foreach (var item in items)
        {
            rebuilt.Append("<!-- wp:list-item -->");
            rebuilt.Append($"<li>{item.InnerHtml.Trim()}</li>");
            rebuilt.Append("<!-- /wp:list-item -->");
        }

        var tag = node.Name.ToLowerInvariant();
        return $"<{tag} class=\"wp-block-list\">{rebuilt}</{tag}>";
    }

    private static bool IsImageOnly(HtmlNode node, out HtmlNode? image)
    {
        image = node.SelectSingleNode(".//img");
        if (image is null) return false;
        return string.IsNullOrWhiteSpace(node.InnerText);
    }

    private static void AppendImage(StringBuilder builder, HtmlNode image)
    {
        var source = image.GetAttributeValue("src", string.Empty);
        var alt = image.GetAttributeValue("alt", string.Empty);
        if (string.IsNullOrWhiteSpace(source)) return;

        var figure = $"<figure class=\"wp-block-image size-full\"><img src=\"{source}\" alt=\"{alt}\"/></figure>";
        Append(builder, "wp:image", figure);
    }

    private static void AppendCode(StringBuilder builder, HtmlNode node)
    {
        var codeNode = node.SelectSingleNode(".//code") ?? node;
        var language = DetectLanguage(node, codeNode);

        var raw = HtmlEntity.DeEntitize(codeNode.InnerHtml ?? string.Empty)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase);

        var stripped = StripTags(raw).TrimEnd();
        var encoded = EscapeForCodeBlock(stripped);

        var attributes = language is null ? string.Empty : $" {{\"language\":\"{language}\"}}";
        var langAttribute = language is null ? string.Empty : $" lang=\"{language}\" class=\"language-{language}\"";

        Append(builder, "wp:code" + attributes,
            $"<pre class=\"wp-block-code\"><code{langAttribute}>{encoded}</code></pre>");
    }

    /// <summary>
    /// Minimal, predictable escaping. Only the three characters that would break the block markup.
    /// </summary>
    private static string EscapeForCodeBlock(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
             .Replace("<", "&lt;", StringComparison.Ordinal)
             .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string StripTags(string value)
    {
        if (!value.Contains('<')) return value;

        var inner = new HtmlDocument();
        inner.LoadHtml(value);
        return inner.DocumentNode.InnerText;
    }

    private static string? DetectLanguage(HtmlNode pre, HtmlNode code)
    {
        var candidates = new[]
        {
            pre.GetAttributeValue("class", string.Empty),
            code.GetAttributeValue("class", string.Empty),
            pre.GetAttributeValue("data-language", string.Empty)
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            // SyntaxHighlighter style: class="brush: csharp; gutter: false"
            var normalized = candidate.Replace("brush:", " ", StringComparison.OrdinalIgnoreCase);
            var tokens = normalized.Split(new[] { ' ', ';', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                var key = token.StartsWith("language-", StringComparison.OrdinalIgnoreCase)
                    ? token["language-".Length..]
                    : token;

                if (LanguageMap.TryGetValue(key, out var mapped)) return mapped;
            }
        }

        return null;
    }

    private static void Append(StringBuilder builder, string blockName, string inner)
    {
        builder.Append("<!-- ").Append(blockName).Append(" -->\n");
        builder.Append(inner).Append('\n');

        var closing = blockName.Split(' ')[0];
        builder.Append("<!-- /").Append(closing).Append(" -->\n\n");
    }
}
