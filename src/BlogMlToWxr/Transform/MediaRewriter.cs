using System.Web;
using BlogMlToWxr.Configuration;
using BlogMlToWxr.Models;
using HtmlAgilityPack;

namespace BlogMlToWxr.Transform;

/// <summary>
/// Rewrites BlogEngine.NET media handlers (image.axd, file.axd, App_Data/files)
/// into a flat WordPress uploads path, and collects every asset that must be copied over.
/// </summary>
public sealed class MediaRewriter
{
    private static readonly string[] UrlAttributes = { "src", "href", "poster", "data-src" };

    private readonly MigrationOptions _options;
    private readonly Dictionary<string, MediaAsset> _assets = new(StringComparer.OrdinalIgnoreCase);

    public MediaRewriter(MigrationOptions options) => _options = options;

    public IReadOnlyCollection<MediaAsset> Assets => _assets.Values;

    public string Rewrite(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        var document = new HtmlDocument { OptionOutputOriginalCase = true };
        document.LoadHtml(html);

        var nodes = document.DocumentNode.SelectNodes("//*[@src or @href or @poster or @data-src]");
        if (nodes is null) return html;

        var changed = false;

        foreach (var node in nodes)
        {
            foreach (var attributeName in UrlAttributes)
            {
                var value = node.GetAttributeValue(attributeName, null);
                if (string.IsNullOrWhiteSpace(value)) continue;

                var rewritten = RewriteUrl(value);
                if (rewritten is null || rewritten == value) continue;

                node.SetAttributeValue(attributeName, rewritten);
                changed = true;
            }
        }

        return changed ? document.DocumentNode.OuterHtml : html;
    }

    /// <summary>
    /// Returns the new URL, or null when the URL is not a local media reference.
    /// </summary>
    public string? RewriteUrl(string url)
    {
        var relativePath = ExtractRelativeMediaPath(url);
        if (relativePath is null) return null;

        var newUrl = _options.MediaBaseUrl + relativePath;

        if (!_assets.ContainsKey(relativePath))
        {
            _assets[relativePath] = new MediaAsset(relativePath, ToAbsolute(url), newUrl);
        }

        return newUrl;
    }

    private string? ExtractRelativeMediaPath(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;
        if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return null;
        if (url.StartsWith('#')) return null;

        // Keep external media untouched.
        if (IsAbsolute(url) && !IsOwnHost(url)) return null;

        var pathAndQuery = StripHost(url);

        // image.axd?picture=/2019/07/diagram.png  and  file.axd?file=/2019/07/slides.pdf
        var questionMark = pathAndQuery.IndexOf('?');
        if (questionMark >= 0)
        {
            var path = pathAndQuery[..questionMark];
            var query = HttpUtility.ParseQueryString(pathAndQuery[(questionMark + 1)..]);

            if (path.EndsWith("image.axd", StringComparison.OrdinalIgnoreCase))
                return Clean(query["picture"]);

            if (path.EndsWith("file.axd", StringComparison.OrdinalIgnoreCase))
                return Clean(query["file"] ?? query["picture"]);

            pathAndQuery = path;
        }

        var appDataMarker = pathAndQuery.IndexOf("App_Data/files/", StringComparison.OrdinalIgnoreCase);
        if (appDataMarker >= 0)
            return Clean(pathAndQuery[(appDataMarker + "App_Data/files/".Length)..]);

        var mediaMarker = pathAndQuery.IndexOf("/media/", StringComparison.OrdinalIgnoreCase);
        if (mediaMarker >= 0 && HasMediaExtension(pathAndQuery))
            return Clean(pathAndQuery[(mediaMarker + "/media/".Length)..]);

        return null;
    }

    private static bool HasMediaExtension(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" or ".bmp"
            or ".pdf" or ".zip" or ".mp4" or ".webm" or ".mp3" or ".docx" or ".pptx" or ".xlsx";
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var decoded = HttpUtility.UrlDecode(value).Replace('\\', '/').TrimStart('~', '/');
        decoded = decoded.Replace("..", string.Empty, StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
    }

    private static bool IsAbsolute(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("//", StringComparison.Ordinal);

    private bool IsOwnHost(string url)
    {
        var normalized = url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var candidate)) return false;
        if (!Uri.TryCreate(_options.SiteUrl, UriKind.Absolute, out var site)) return false;

        return candidate.Host.Equals(site.Host, StringComparison.OrdinalIgnoreCase)
            || candidate.Host.Equals("www." + site.Host, StringComparison.OrdinalIgnoreCase)
            || ("www." + candidate.Host).Equals(site.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripHost(string url)
    {
        var normalized = url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absolute))
            return absolute.PathAndQuery;

        return normalized;
    }

    private string ToAbsolute(string url)
    {
        if (IsAbsolute(url))
            return url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;

        var relative = url.StartsWith('/') ? url : "/" + url.TrimStart('~', '/');
        return _options.SiteUrl + relative;
    }
}
