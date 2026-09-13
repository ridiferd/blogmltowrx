using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlogMlToWxr.Configuration;

/// <summary>
/// Runtime configuration. Loaded from appsettings.json, then overridden by command line switches.
/// </summary>
public sealed class MigrationOptions
{
    public string SourceBlogMlPath { get; set; } = "input/BlogML.xml";
    public string OutputDirectory { get; set; } = "output";

    public string SiteUrl { get; set; } = "https://example.com";
    public string NewPostUrlPattern { get; set; } = "/post/{slug}/";
    public string Language { get; set; } = "en-US";

    public string MediaBaseUrl { get; set; } = "/wp-content/uploads/blog/";
    public bool DownloadMedia { get; set; } = true;
    public int MediaDownloadDelayMs { get; set; } = 150;

    public string DefaultAuthorLogin { get; set; } = "admin";
    public string DefaultAuthorEmail { get; set; } = "admin@example.com";
    public string DefaultAuthorDisplayName { get; set; } = "Admin";

    public bool ConvertToGutenberg { get; set; } = true;
    public bool IncludeComments { get; set; } = true;
    public int MaxItemsPerFile { get; set; } = 200;

    [JsonIgnore]
    public string MediaOutputDirectory => Path.Combine(OutputDirectory, "media");

    public static MigrationOptions Load(string[] args)
    {
        var settingsPath = FindArg(args, "--settings") ?? "appsettings.json";
        MigrationOptions options;

        if (File.Exists(settingsPath))
        {
            var json = File.ReadAllText(settingsPath);
            options = JsonSerializer.Deserialize<MigrationOptions>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new MigrationOptions();
        }
        else
        {
            Console.WriteLine($"[warn] {settingsPath} not found, using defaults plus command line switches.");
            options = new MigrationOptions();
        }

        options.SourceBlogMlPath = FindArg(args, "--input") ?? options.SourceBlogMlPath;
        options.OutputDirectory = FindArg(args, "--output") ?? options.OutputDirectory;
        options.SiteUrl = FindArg(args, "--site") ?? options.SiteUrl;
        options.MediaBaseUrl = FindArg(args, "--media-base") ?? options.MediaBaseUrl;

        if (HasFlag(args, "--no-media")) options.DownloadMedia = false;
        if (HasFlag(args, "--download-media")) options.DownloadMedia = true;
        if (HasFlag(args, "--no-gutenberg")) options.ConvertToGutenberg = false;
        if (HasFlag(args, "--no-comments")) options.IncludeComments = false;

        var maxItems = FindArg(args, "--max-items");
        if (int.TryParse(maxItems, out var parsed) && parsed > 0) options.MaxItemsPerFile = parsed;

        options.SiteUrl = options.SiteUrl.TrimEnd('/');
        if (!options.MediaBaseUrl.EndsWith('/')) options.MediaBaseUrl += "/";

        return options;
    }

    private static string? FindArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
}
