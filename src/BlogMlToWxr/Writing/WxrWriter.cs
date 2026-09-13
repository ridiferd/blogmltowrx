using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using BlogMlToWxr.Configuration;
using BlogMlToWxr.Models;
using BlogMlToWxr.Transform;

namespace BlogMlToWxr.Writing;

/// <summary>
/// Emits WordPress eXtended RSS 1.2, the exact format Tools &gt; Import expects.
/// </summary>
public sealed class WxrWriter
{
    private static readonly XNamespace Excerpt = "http://wordpress.org/export/1.2/excerpt/";
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private static readonly XNamespace Wfw = "http://wellformedweb.org/CommentAPI/";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Wp = "http://wordpress.org/export/1.2/";

    private readonly MigrationOptions _options;

    public WxrWriter(MigrationOptions options) => _options = options;

    public IReadOnlyList<string> Write(BlogData blog, IReadOnlyDictionary<string, string> renderedContent)
    {
        Directory.CreateDirectory(_options.OutputDirectory);

        var files = new List<string>();
        var batches = blog.Posts
            .Select((post, index) => new { post, index })
            .GroupBy(x => x.index / _options.MaxItemsPerFile)
            .ToList();

        var postId = 1000;
        var commentId = 1;

        foreach (var batch in batches)
        {
            var rss = BuildDocument(blog, batch.Key == 0);
            var channel = rss.Root!.Element("channel")!;

            foreach (var entry in batch)
            {
                var post = entry.post;
                var body = renderedContent.TryGetValue(post.Slug, out var rendered) ? rendered : post.Content;
                channel.Add(BuildItem(blog, post, body, ++postId, ref commentId));
            }

            var suffix = batches.Count == 1 ? string.Empty : $"-part{batch.Key + 1:00}";
            var path = Path.Combine(_options.OutputDirectory, $"ridilabs-wxr{suffix}.xml");
            SaveWithCData(rss, path);
            files.Add(path);
        }

        return files;
    }

    private XDocument BuildDocument(BlogData blog, bool includeTaxonomy)
    {
        var channel = new XElement("channel",
            new XElement("title", blog.Title),
            new XElement("link", _options.SiteUrl),
            new XElement("description", blog.SubTitle),
            new XElement("pubDate", DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture)),
            new XElement("language", _options.Language),
            new XElement(Wp + "wxr_version", "1.2"),
            new XElement(Wp + "base_site_url", _options.SiteUrl),
            new XElement(Wp + "base_blog_url", _options.SiteUrl),
            BuildAuthor());

        if (includeTaxonomy)
        {
            var termId = 100;

            foreach (var category in blog.Categories)
            {
                channel.Add(new XElement(Wp + "category",
                    new XElement(Wp + "term_id", ++termId),
                    new XElement(Wp + "category_nicename", new XCData(category.Slug)),
                    new XElement(Wp + "category_parent", new XCData(string.Empty)),
                    new XElement(Wp + "cat_name", new XCData(category.Name))));
            }

            var tags = blog.Posts
                .SelectMany(p => p.Tags)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase);

            foreach (var tag in tags)
            {
                channel.Add(new XElement(Wp + "tag",
                    new XElement(Wp + "term_id", ++termId),
                    new XElement(Wp + "tag_slug", new XCData(SlugFactory.Normalize(tag))),
                    new XElement(Wp + "tag_name", new XCData(tag))));
            }
        }

        var rss = new XElement("rss",
            new XAttribute("version", "2.0"),
            new XAttribute(XNamespace.Xmlns + "excerpt", Excerpt),
            new XAttribute(XNamespace.Xmlns + "content", Content),
            new XAttribute(XNamespace.Xmlns + "wfw", Wfw),
            new XAttribute(XNamespace.Xmlns + "dc", Dc),
            new XAttribute(XNamespace.Xmlns + "wp", Wp),
            channel);

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), rss);
    }

    private XElement BuildAuthor() =>
        new(Wp + "author",
            new XElement(Wp + "author_id", 1),
            new XElement(Wp + "author_login", new XCData(_options.DefaultAuthorLogin)),
            new XElement(Wp + "author_email", new XCData(_options.DefaultAuthorEmail)),
            new XElement(Wp + "author_display_name", new XCData(_options.DefaultAuthorDisplayName)),
            new XElement(Wp + "author_first_name", new XCData(string.Empty)),
            new XElement(Wp + "author_last_name", new XCData(string.Empty)));

    private XElement BuildItem(BlogData blog, BlogPost post, string body, int postId, ref int commentId)
    {
        var permalink = _options.SiteUrl + _options.NewPostUrlPattern.Replace("{slug}", post.Slug, StringComparison.Ordinal);
        var created = post.DateCreated;
        var createdUtc = created.ToUniversalTime();

        var item = new XElement("item",
            new XElement("title", Sanitize(post.Title)),
            new XElement("link", permalink),
            new XElement("pubDate", created.ToString("r", CultureInfo.InvariantCulture)),
            new XElement(Dc + "creator", new XCData(_options.DefaultAuthorLogin)),
            new XElement("guid", new XAttribute("isPermaLink", "false"), $"{_options.SiteUrl}/?p={postId}"),
            new XElement("description", string.Empty),
            new XElement(Content + "encoded", new XCData(Sanitize(body))),
            new XElement(Excerpt + "encoded", new XCData(Sanitize(post.Excerpt))),
            new XElement(Wp + "post_id", postId),
            new XElement(Wp + "post_date", new XCData(created.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))),
            new XElement(Wp + "post_date_gmt", new XCData(createdUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))),
            new XElement(Wp + "comment_status", new XCData("open")),
            new XElement(Wp + "ping_status", new XCData("closed")),
            new XElement(Wp + "post_name", new XCData(post.Slug)),
            new XElement(Wp + "status", new XCData(post.Approved ? "publish" : "draft")),
            new XElement(Wp + "post_parent", 0),
            new XElement(Wp + "menu_order", 0),
            new XElement(Wp + "post_type", new XCData(post.IsPage ? "page" : "post")),
            new XElement(Wp + "post_password", new XCData(string.Empty)),
            new XElement(Wp + "is_sticky", 0));

        foreach (var reference in post.CategoryRefs)
        {
            var category = blog.Categories.FirstOrDefault(c =>
                c.Id.Equals(reference, StringComparison.OrdinalIgnoreCase));
            if (category is null) continue;

            item.Add(new XElement("category",
                new XAttribute("domain", "category"),
                new XAttribute("nicename", category.Slug),
                new XCData(category.Name)));
        }

        foreach (var tag in post.Tags.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            item.Add(new XElement("category",
                new XAttribute("domain", "post_tag"),
                new XAttribute("nicename", SlugFactory.Normalize(tag)),
                new XCData(tag)));
        }

        if (!string.IsNullOrWhiteSpace(post.OriginalUrl))
        {
            item.Add(new XElement(Wp + "postmeta",
                new XElement(Wp + "meta_key", new XCData("_blogengine_original_url")),
                new XElement(Wp + "meta_value", new XCData(post.OriginalUrl))));
        }

        if (_options.IncludeComments)
        {
            foreach (var comment in post.Comments)
            {
                item.Add(BuildComment(comment, commentId++));
            }
        }

        return item;
    }

    private XElement BuildComment(BlogComment comment, int id) =>
        new(Wp + "comment",
            new XElement(Wp + "comment_id", id),
            new XElement(Wp + "comment_author", new XCData(Sanitize(comment.AuthorName))),
            new XElement(Wp + "comment_author_email", new XCData(comment.AuthorEmail)),
            new XElement(Wp + "comment_author_url", comment.AuthorUrl),
            new XElement(Wp + "comment_author_IP", new XCData(string.Empty)),
            new XElement(Wp + "comment_date", new XCData(comment.DateCreated.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))),
            new XElement(Wp + "comment_date_gmt", new XCData(comment.DateCreated.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))),
            new XElement(Wp + "comment_content", new XCData(Sanitize(comment.Content))),
            new XElement(Wp + "comment_approved", new XCData(comment.Approved ? "1" : "0")),
            new XElement(Wp + "comment_type", new XCData(string.Empty)),
            new XElement(Wp + "comment_parent", 0),
            new XElement(Wp + "comment_user_id", 0));

    /// <summary>
    /// Removes control characters that are legal in BlogEngine storage but fatal to the WordPress importer.
    /// </summary>
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (XmlConvert.IsXmlChar(character)) builder.Append(character);
        }

        return builder.ToString();
    }

    private static void SaveWithCData(XDocument document, string path)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(false),
            NewLineHandling = NewLineHandling.None
        };

        using var writer = XmlWriter.Create(path, settings);
        document.Save(writer);
    }
}
