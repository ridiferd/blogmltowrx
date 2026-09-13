# BlogMlToWxr

Converts a **BlogEngine.NET** BlogML export into a **WordPress WXR 1.2** file, with zero content loss as the design goal.

Built for the ridilabs.net migration, but the settings file makes it reusable for any BlogEngine site.

---

## What it produces

Running the tool writes everything into `output/`:

| File | Purpose |
| --- | --- |
| `ridilabs-wxr.xml` | The WXR import file (split into `-part01`, `-part02` when large) |
| `redirects.csv` | Old to new URL map, ready for the **Redirection** plugin |
| `redirects.htaccess` | The same map as Apache 301 rules, faster than the plugin |
| `media-manifest.csv` | Every referenced asset, its old URL and its new URL |
| `media/` or `media-flat/` | Downloaded assets, ready to upload. Flat mode splits them into `batch-XX` folders of 100 |
| `report.md` | Full post inventory plus warnings, use this to verify counts |

## What it handles

- Posts, pages, drafts, categories, tags, comments, authors, original publish dates
- Original slugs preserved, so permalinks can stay identical
- `image.axd?picture=`, `file.axd?file=`, and `App_Data/files/` rewritten to `/wp-content/uploads/ridilabs/`
- Code blocks converted to real Gutenberg `wp:code` blocks, including SyntaxHighlighter `brush: csharp` language detection
- Headings, lists, quotes, tables, images converted to block markup instead of one large classic block
- Control characters that break the WordPress importer are stripped
- Namespace inconsistencies between BlogEngine versions handled by matching local element names

---

## Quick start

```powershell
git clone <your repo>   # or unzip
cd BlogMlToWxr
dotnet restore
```

Open `BlogMlToWxr.sln` in **Visual Studio 2022** (17.8 or newer, .NET 8 SDK), or use **VS Code** with the C# Dev Kit.

1. Drop your export at `src/BlogMlToWxr/input/BlogML.xml`
2. Edit `src/BlogMlToWxr/appsettings.json`
3. Run:

```powershell
cd src/BlogMlToWxr
dotnet run
```

Try it against the bundled sample first:

```powershell
dotnet run -- --input ../../samples/BlogML-sample.xml --output ../../output-sample --no-media
```

### Command line switches

```
--input <path>       BlogML XML file
--output <dir>       Output directory
--site <url>         Old site root, https://ridilabs.net
--media-mode <mode>  path (keep folders, needs FTP) or flat (Media Library upload)
--media-base <path>  Target uploads path
--max-items <n>      Items per WXR file
--download-media     Fetch assets from the live site
--no-media           Manifest only
--no-gutenberg       Keep classic HTML
--no-comments        Drop comments
```

---

## Import checklist

**Before import**

1. Fresh WordPress on the shared hosting, no demo content.
2. Settings > Permalinks set to a custom structure of `/post/%postname%/` so the old shape survives.
3. Create the author account whose login matches `DefaultAuthorLogin`.
4. Install plugins: **WordPress Importer**, **Redirection**, **Add From Server** (or use WP-CLI).

**Media first, always**

Pick the track that matches your hosting access.

*Track A, wp-admin only, no FTP and no File Manager (this is the ridilabs.net track)*

Run the converter with `"MediaMode": "flat"` and `"MediaBaseUrl": "/wp-content/uploads/"`. Every asset is collapsed into one folder with WordPress safe, collision free file names, split into `batch-01`, `batch-02` and so on at 100 files each.

5. In wp-admin, go to **Settings > Media** and **uncheck** "Organize my uploads into month- and year-based folders". Save. Without this, WordPress files everything under `/wp-content/uploads/2026/09/` and the rewritten URLs will not match.
6. Go to **Media > Add New** and drag one `batch-XX` folder at a time into the uploader. Wait for each batch to finish before starting the next.
7. Spot check a few URLs, for example `https://yoursite/wp-content/uploads/pipeline.png`, before moving on.
8. Leave the month and year option **off** until the WXR import is done. You can turn it back on afterwards, existing files are not moved.

Why not let WordPress download the images from the old site instead: BlogEngine serves media through `image.axd?picture=...`, so the sideloader derives the file name `image.axd`, and WordPress rejects that extension. Every sideload plugin hits the same wall.

*Track B, FTP, SSH or cPanel File Manager available*

Run with `"MediaMode": "path"` and `"MediaBaseUrl": "/wp-content/uploads/ridilabs/"`, then copy `output/media/` into `wp-content/uploads/ridilabs/` and register the files:

```bash
wp media import wp-content/uploads/ridilabs/**/*.{png,jpg,jpeg,gif,pdf} --skip-copy
```

Either way, media goes in before the posts, so the imported HTML resolves immediately and nothing shows a broken image.

**Then content**

9. Tools > Import > WordPress, upload `ridilabs-wxr.xml`. If the host caps uploads below the file size, rerun with `--max-items 100` and import the parts in order.
10. On the mapping screen, assign all posts to the existing author. Leave "download and import file attachments" **unchecked**, the media is already in place.

**Then redirects**

11. Either paste `redirects.htaccess` above the WordPress block in `.htaccess` (fastest), or import `redirects.csv` in Redirection > Import/Export.

**Then verify**

12. Compare the post count against `report.md`.
13. Crawl the new site with Screaming Frog, filter on 404 and on redirect chains.
14. Submit the new sitemap in Search Console and keep the old property live for 6 months.

---

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| Importer stops halfway with no error | PHP timeout. Split with `--max-items 50` or run `wp import` over SSH. |
| Code blocks render as plain paragraphs | The source used an unrecognised highlighter class. Check `report.md` warnings, extend `LanguageMap` in `GutenbergConverter.cs`. |
| Images 404 after import | The uploads path does not match `MediaBaseUrl`. In flat mode this almost always means the month and year folder option was still on during upload. |
| Media Library shows `pipeline-1.png` | Two source folders held the same file name. That is expected, the converter already pointed the affected posts at the deduplicated name. |
| Redirection plugin unavailable, no `.htaccess` access | Ask the host to paste `redirects.htaccess`, or install Redirection from wp-admin and import `redirects.csv`. |
| Some assets missing in `media-manifest.csv` | Those URLs were external or used a handler not covered. Add the pattern in `MediaRewriter.ExtractRelativeMediaPath`. |
| Dates shifted by hours | BlogML stores local time. The tool writes both local and GMT fields, confirm the WordPress timezone setting is Asia/Jakarta. |

---

## Project layout

```
src/BlogMlToWxr/
  Configuration/MigrationOptions.cs   settings, CLI switches, media mode
  Models/BlogModels.cs                domain objects
  Reading/BlogMlReader.cs             BlogML parser
  Reading/XmlExtensions.cs            namespace tolerant lookups
  Transform/SlugFactory.cs            unique WordPress slugs
  Transform/MediaRewriter.cs          axd rewriting, flat name generation, asset collection
  Transform/GutenbergConverter.cs     classic HTML to block markup
  Writing/WxrWriter.cs                WXR 1.2 emitter
  Writing/RedirectWriter.cs           csv and htaccess 301 map
  Writing/MediaDownloader.cs          asset fetcher and manifest
  Reporting/MigrationReport.cs        counts, inventory, warnings
  Program.cs                          pipeline
```

Single dependency: HtmlAgilityPack.
