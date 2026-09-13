using System.Xml;
using System.Xml.Linq;
using BlogMlToWxr.Models;
using BlogMlToWxr.Transform;

namespace BlogMlToWxr.Reading;

public sealed class BlogMlReader
{
    private readonly SlugFactory _slugFactory = new();

    public BlogData Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"BlogML export not found: {Path.GetFullPath(path)}");

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = false,
            CheckCharacters = false
        };

        using var stream = File.OpenRead(path);
        using var reader = XmlReader.Create(stream, settings);
        var doc = XDocument.Load(reader, LoadOptions.None);

        var root = doc.Root ?? throw new InvalidDataException("BlogML export has no root element.");

        var blog = new BlogData
        {
            RootUrl = root.Attr("root-url"),
            DateCreated = root.AttrDate("date-created", DateTime.UtcNow),
            Title = root.ChildValue("title"),
            SubTitle = root.ChildValue("sub-title")
        };

        if (string.IsNullOrWhiteSpace(blog.Title)) blog.Title = "Blog";

        ReadAuthors(root, blog);
        ReadCategories(root, blog);
        ReadPosts(root, blog);

        return blog;
    }

    private static void ReadAuthors(XElement root, BlogData blog)
    {
        var authors = root.Child("authors");
        if (authors is null) return;

        foreach (var element in authors.Children("author"))
        {
            blog.Authors.Add(new BlogAuthor
            {
                Id = element.Attr("id"),
                Email = element.Attr("email"),
                Name = element.ChildValue("title")
            });
        }
    }

    private void ReadCategories(XElement root, BlogData blog)
    {
        var categories = root.Child("categories");
        if (categories is null) return;

        var slugs = new SlugFactory();

        foreach (var element in categories.Children("category"))
        {
            var name = element.ChildValue("title");
            if (string.IsNullOrWhiteSpace(name)) continue;

            blog.Categories.Add(new BlogCategory
            {
                Id = element.Attr("id"),
                Name = name,
                Slug = slugs.Create(name),
                ParentId = element.Attr("parentref")
            });
        }
    }

    private void ReadPosts(XElement root, BlogData blog)
    {
        var posts = root.Child("posts");
        if (posts is null) return;

        foreach (var element in posts.Children("post"))
        {
            var title = element.ChildValue("title");
            var postName = element.ChildValue("post-name");
            var slugSeed = !string.IsNullOrWhiteSpace(postName) ? postName : title;

            var post = new BlogPost
            {
                Id = element.Attr("id"),
                Title = string.IsNullOrWhiteSpace(title) ? "Untitled" : title,
                Slug = _slugFactory.Create(slugSeed),
                Content = element.Child("content")?.Value ?? string.Empty,
                Excerpt = element.Child("excerpt")?.Value?.Trim() ?? string.Empty,
                OriginalUrl = element.Attr("post-url"),
                DateCreated = element.AttrDate("date-created", DateTime.UtcNow),
                DateModified = element.AttrDate("date-modified", DateTime.UtcNow),
                Approved = element.AttrBool("approved", true),
                IsPage = element.Attr("type").Equals("page", StringComparison.OrdinalIgnoreCase)
            };

            var postCategories = element.Child("categories");
            if (postCategories is not null)
            {
                foreach (var reference in postCategories.Children("category"))
                {
                    var id = reference.Attr("ref");
                    if (!string.IsNullOrWhiteSpace(id)) post.CategoryRefs.Add(id);
                }
            }

            var postTags = element.Child("tags");
            if (postTags is not null)
            {
                foreach (var tag in postTags.Children("tag"))
                {
                    var value = tag.Attr("ref");
                    if (string.IsNullOrWhiteSpace(value)) value = tag.ChildValue("title");
                    if (!string.IsNullOrWhiteSpace(value)) post.Tags.Add(value.Trim());
                }
            }

            var postAuthors = element.Child("authors");
            if (postAuthors is not null)
            {
                post.AuthorRef = postAuthors.Children("author").FirstOrDefault()?.Attr("ref") ?? string.Empty;
            }

            var comments = element.Child("comments");
            if (comments is not null)
            {
                foreach (var comment in comments.Children("comment"))
                {
                    post.Comments.Add(new BlogComment
                    {
                        Id = comment.Attr("id"),
                        AuthorName = comment.Attr("user-name"),
                        AuthorEmail = comment.Attr("user-email"),
                        AuthorUrl = comment.Attr("user-url"),
                        Content = comment.Child("content")?.Value?.Trim() ?? string.Empty,
                        DateCreated = comment.AttrDate("date-created", post.DateCreated),
                        Approved = comment.AttrBool("approved", true)
                    });
                }
            }

            blog.Posts.Add(post);
        }
    }
}
