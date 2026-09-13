using System.Text;
using BlogMlToWxr.Configuration;
using BlogMlToWxr.Models;

namespace BlogMlToWxr.Reporting;

public sealed class MigrationReport
{
    public int PostCount { get; set; }
    public int PageCount { get; set; }
    public int DraftCount { get; set; }
    public int CommentCount { get; set; }
    public int CategoryCount { get; set; }
    public int TagCount { get; set; }
    public int MediaCount { get; set; }
    public int MediaDownloaded { get; set; }
    public int RedirectCount { get; set; }
    public List<string> WxrFiles { get; } = new();
    public List<string> Warnings { get; } = new();

    public void Print()
    {
        Console.WriteLine();
        Console.WriteLine("Migration summary");
        Console.WriteLine("-----------------");
        Console.WriteLine($"  Posts          : {PostCount} (drafts: {DraftCount})");
        Console.WriteLine($"  Pages          : {PageCount}");
        Console.WriteLine($"  Comments       : {CommentCount}");
        Console.WriteLine($"  Categories     : {CategoryCount}");
        Console.WriteLine($"  Tags           : {TagCount}");
        Console.WriteLine($"  Media assets   : {MediaCount} (downloaded: {MediaDownloaded})");
        Console.WriteLine($"  Redirect rules : {RedirectCount}");
        Console.WriteLine($"  WXR files      : {WxrFiles.Count}");

        foreach (var file in WxrFiles) Console.WriteLine($"      {file}");

        if (Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  Warnings ({Warnings.Count}):");
            foreach (var warning in Warnings.Take(20)) Console.WriteLine($"      {warning}");
            if (Warnings.Count > 20) Console.WriteLine($"      ... and {Warnings.Count - 20} more, see report.md");
        }
    }

    public void Save(MigrationOptions options, BlogData blog)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Migration report");
        builder.AppendLine();
        builder.AppendLine($"Source blog: {blog.Title}");
        builder.AppendLine($"Generated  : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine("| Item | Count |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Posts | {PostCount} |");
        builder.AppendLine($"| Drafts | {DraftCount} |");
        builder.AppendLine($"| Pages | {PageCount} |");
        builder.AppendLine($"| Comments | {CommentCount} |");
        builder.AppendLine($"| Categories | {CategoryCount} |");
        builder.AppendLine($"| Tags | {TagCount} |");
        builder.AppendLine($"| Media assets | {MediaCount} |");
        builder.AppendLine($"| Media downloaded | {MediaDownloaded} |");
        builder.AppendLine($"| Redirect rules | {RedirectCount} |");
        builder.AppendLine();

        builder.AppendLine("## Post inventory");
        builder.AppendLine();
        builder.AppendLine("| Date | Status | Slug | Title |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var post in blog.Posts.OrderByDescending(p => p.DateCreated))
        {
            var title = post.Title.Replace("|", "\\|", StringComparison.Ordinal);
            builder.AppendLine($"| {post.DateCreated:yyyy-MM-dd} | {(post.Approved ? "publish" : "draft")} | {post.Slug} | {title} |");
        }

        if (Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Warnings");
            builder.AppendLine();
            foreach (var warning in Warnings) builder.AppendLine($"- {warning}");
        }

        File.WriteAllText(Path.Combine(options.OutputDirectory, "report.md"), builder.ToString(), new UTF8Encoding(false));
    }
}
