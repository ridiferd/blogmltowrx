namespace BlogMlToWxr.Models;

public sealed class BlogData
{
    public string Title { get; set; } = "Blog";
    public string SubTitle { get; set; } = "";
    public string RootUrl { get; set; } = "";
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public List<BlogAuthor> Authors { get; } = new();
    public List<BlogCategory> Categories { get; } = new();
    public List<BlogPost> Posts { get; } = new();
}

public sealed class BlogAuthor
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class BlogCategory
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string ParentId { get; set; } = "";
}

public sealed class BlogComment
{
    public string Id { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string AuthorEmail { get; set; } = "";
    public string AuthorUrl { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public bool Approved { get; set; } = true;
}

public sealed class BlogPost
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Content { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public string OriginalUrl { get; set; } = "";
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateModified { get; set; } = DateTime.UtcNow;
    public bool Approved { get; set; } = true;
    public bool IsPage { get; set; }
    public List<string> CategoryRefs { get; } = new();
    public List<string> Tags { get; } = new();
    public List<BlogComment> Comments { get; } = new();
    public string AuthorRef { get; set; } = "";
}

public sealed class MediaAsset
{
    public MediaAsset(string relativePath, string originalAbsoluteUrl, string newUrl)
    {
        RelativePath = relativePath;
        OriginalAbsoluteUrl = originalAbsoluteUrl;
        NewUrl = newUrl;
    }

    public string RelativePath { get; }
    public string OriginalAbsoluteUrl { get; }
    public string NewUrl { get; }
    public bool Downloaded { get; set; }
    public string? Error { get; set; }
}
