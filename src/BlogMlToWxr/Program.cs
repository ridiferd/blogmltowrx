using BlogMlToWxr.Configuration;
using BlogMlToWxr.Reading;
using BlogMlToWxr.Reporting;
using BlogMlToWxr.Transform;
using BlogMlToWxr.Writing;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Any(a => a is "--help" or "-h" or "/?"))
{
    PrintHelp();
    return 0;
}

try
{
    var options = MigrationOptions.Load(args);

    Console.WriteLine("BlogML to WordPress WXR converter");
    Console.WriteLine($"  input      : {Path.GetFullPath(options.SourceBlogMlPath)}");
    Console.WriteLine($"  output     : {Path.GetFullPath(options.OutputDirectory)}");
    Console.WriteLine($"  site       : {options.SiteUrl}");
    Console.WriteLine($"  media base : {options.MediaBaseUrl}");
    Console.WriteLine($"  media mode : {options.MediaMode}{(options.FlatMedia ? "  (Media Library drag and drop, no FTP needed)" : "  (preserves year/month folders)")}");
    Console.WriteLine();

    Directory.CreateDirectory(options.OutputDirectory);

    Console.WriteLine("[1/5] Reading BlogML export");
    var blog = new BlogMlReader().Read(options.SourceBlogMlPath);
    Console.WriteLine($"      {blog.Posts.Count} entries found");

    Console.WriteLine("[2/5] Rewriting media URLs and converting content");
    var mediaRewriter = new MediaRewriter(options);
    var gutenberg = new GutenbergConverter();
    var report = new MigrationReport();
    var rendered = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var post in blog.Posts)
    {
        var html = mediaRewriter.Rewrite(post.Content);

        if (options.ConvertToGutenberg)
        {
            try
            {
                html = gutenberg.Convert(html);
            }
            catch (Exception ex)
            {
                report.Warnings.Add($"Gutenberg conversion failed for '{post.Slug}', kept classic HTML: {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(html))
            report.Warnings.Add($"Empty content after conversion: {post.Slug}");

        if (!string.IsNullOrWhiteSpace(post.Excerpt))
            post.Excerpt = mediaRewriter.Rewrite(post.Excerpt);

        rendered[post.Slug] = html;
    }

    Console.WriteLine($"      {mediaRewriter.Assets.Count} media assets referenced");

    Console.WriteLine("[3/5] Writing WXR");
    var wxrFiles = new WxrWriter(options).Write(blog, rendered);

    Console.WriteLine("[4/5] Writing redirect map");
    var redirects = new RedirectWriter(options).Write(blog);

    Console.WriteLine("[5/5] Handling media");
    var downloader = new MediaDownloader(options);
    var downloaded = 0;

    if (options.DownloadMedia)
    {
        downloaded = await downloader.DownloadAsync(mediaRewriter.Assets);
    }
    else
    {
        downloader.WriteManifest(mediaRewriter.Assets);
        Console.WriteLine("      download skipped, manifest written only");
    }

    report.PostCount = blog.Posts.Count(p => !p.IsPage);
    report.PageCount = blog.Posts.Count(p => p.IsPage);
    report.DraftCount = blog.Posts.Count(p => !p.Approved);
    report.CommentCount = blog.Posts.Sum(p => p.Comments.Count);
    report.CategoryCount = blog.Categories.Count;
    report.TagCount = blog.Posts.SelectMany(p => p.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    report.MediaCount = mediaRewriter.Assets.Count;
    report.MediaDownloaded = downloaded;
    report.RedirectCount = redirects.Count;
    report.WxrFiles.AddRange(wxrFiles);

    report.Save(options, blog);
    report.Print();

    Console.WriteLine();
    Console.WriteLine("Next steps are described in README.md, section 'Import checklist'.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"FAILED: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
        BlogMlToWxr - convert a BlogEngine.NET BlogML export into WordPress WXR.

        Usage:
          BlogMlToWxr [options]

        Options:
          --settings <path>    Settings file (default appsettings.json)
          --input <path>       BlogML XML file
          --output <dir>       Output directory
          --site <url>         Old site root, for example https://ridilabs.net
          --media-base <path>  Target uploads path, default /wp-content/uploads/ridilabs/
          --media-mode <mode>  path (keep folders, needs FTP) or flat (Media Library upload)
          --max-items <n>      Items per WXR file, keeps each file under the upload limit
          --download-media     Fetch every referenced asset from the live site
          --no-media           Skip downloading, write the manifest only
          --no-gutenberg       Keep classic HTML instead of block markup
          --no-comments        Drop comments
          --help               Show this help
        """);
}
