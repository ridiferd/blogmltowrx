using System.Text;
using BlogMlToWxr.Configuration;
using BlogMlToWxr.Models;

namespace BlogMlToWxr.Writing;

/// <summary>
/// Downloads every referenced asset from the live BlogEngine site into a folder
/// whose structure mirrors the target wp-content/uploads path, ready for a single FTP upload.
/// </summary>
public sealed class MediaDownloader
{
    private readonly MigrationOptions _options;

    public MediaDownloader(MigrationOptions options) => _options = options;

    public async Task<int> DownloadAsync(IReadOnlyCollection<MediaAsset> assets, CancellationToken cancellationToken = default)
    {
        if (assets.Count == 0) return 0;

        Directory.CreateDirectory(_options.MediaOutputDirectory);

        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("BlogMlToWxr/1.0 (migration tool)");

        var succeeded = 0;
        var index = 0;

        foreach (var asset in assets)
        {
            index++;
            // In flat mode the files are split into batches of 100 so the Media Library
            // drag and drop uploader can swallow them without a browser timeout.
            var destination = _options.FlatMedia
                ? Path.Combine(_options.MediaOutputDirectory, $"batch-{((index - 1) / 100) + 1:00}", asset.TargetPath)
                : Path.Combine(_options.MediaOutputDirectory, asset.TargetPath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            if (File.Exists(destination))
            {
                asset.Downloaded = true;
                succeeded++;
                continue;
            }

            try
            {
                using var response = await client.GetAsync(asset.OriginalAbsoluteUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    asset.Error = $"HTTP {(int)response.StatusCode}";
                    Console.WriteLine($"  [{index}/{assets.Count}] miss {asset.RelativePath} ({asset.Error})");
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var file = File.Create(destination);
                await stream.CopyToAsync(file, cancellationToken);

                asset.Downloaded = true;
                succeeded++;

                if (index % 25 == 0) Console.WriteLine($"  [{index}/{assets.Count}] downloaded");
            }
            catch (Exception ex)
            {
                asset.Error = ex.Message;
                Console.WriteLine($"  [{index}/{assets.Count}] fail {asset.RelativePath}: {ex.Message}");
            }

            if (_options.MediaDownloadDelayMs > 0)
                await Task.Delay(_options.MediaDownloadDelayMs, cancellationToken);
        }

        WriteManifest(assets);
        return succeeded;
    }

    public void WriteManifest(IReadOnlyCollection<MediaAsset> assets)
    {
        var manifest = new StringBuilder("source_path,target_path,original_url,new_url,downloaded,error\n");

        foreach (var asset in assets)
        {
            manifest.Append(asset.RelativePath).Append(',')
                    .Append(asset.TargetPath).Append(',')
                    .Append(asset.OriginalAbsoluteUrl).Append(',')
                    .Append(asset.NewUrl).Append(',')
                    .Append(asset.Downloaded ? "yes" : "no").Append(',')
                    .Append((asset.Error ?? string.Empty).Replace(',', ';')).Append('\n');
        }

        File.WriteAllText(Path.Combine(_options.OutputDirectory, "media-manifest.csv"), manifest.ToString(), new UTF8Encoding(false));
    }
}
