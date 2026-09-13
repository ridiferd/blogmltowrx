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
| `media/` | Downloaded assets, mirroring the target uploads folder structure |
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

5. FTP `output/media/` into `wp-content/uploads/ridilabs/` on the server.
6. Register the files in the Media Library:

```bash
wp media import wp-content/uploads/ridilabs/**/*.{png,jpg,jpeg,gif,pdf} --skip-copy
```

Doing media before the posts means the imported HTML resolves immediately and nothing shows a broken image.

**Then content**

7. Tools > Import > WordPress, upload `ridilabs-wxr.xml`. If the host caps uploads below the file size, rerun with `--max-items 100` and import the parts in order.
8. On the mapping screen, assign all posts to the existing author. Leave "download and import file attachments" **unchecked**, the media is already in place.

**Then redirects**

9. Either paste `redirects.htaccess` above the WordPress block in `.htaccess` (fastest), or import `redirects.csv` in Redirection > Import/Export.

**Then verify**

10. Compare the post count against `report.md`.
11. Crawl the new site with Screaming Frog, filter on 404 and on redirect chains.
12. Submit the new sitemap in Search Console and keep the old property live for 6 months.

---

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| Importer stops halfway with no error | PHP timeout. Split with `--max-items 50` or run `wp import` over SSH. |
| Code blocks render as plain paragraphs | The source used an unrecognised highlighter class. Check `report.md` warnings, extend `LanguageMap` in `GutenbergConverter.cs`. |
| Images 404 after import | The media folder was uploaded under a different path than `MediaBaseUrl`. The two must match exactly. |
| Some assets missing in `media-manifest.csv` | Those URLs were external or used a handler not covered. Add the pattern in `MediaRewriter.ExtractRelativeMediaPath`. |
| Dates shifted by hours | BlogML stores local time. The tool writes both local and GMT fields, confirm the WordPress timezone setting is Asia/Jakarta. |

---

## Project layout

```
src/BlogMlToWxr/
  Configuration/MigrationOptions.cs   settings and CLI switches
  Models/BlogModels.cs                domain objects
  Reading/BlogMlReader.cs             BlogML parser
  Reading/XmlExtensions.cs            namespace tolerant lookups
  Transform/SlugFactory.cs            unique WordPress slugs
  Transform/MediaRewriter.cs          axd handler rewriting, asset collection
  Transform/GutenbergConverter.cs     classic HTML to block markup
  Writing/WxrWriter.cs                WXR 1.2 emitter
  Writing/RedirectWriter.cs           csv and htaccess 301 map
  Writing/MediaDownloader.cs          asset fetcher and manifest
  Reporting/MigrationReport.cs        counts, inventory, warnings
  Program.cs                          pipeline
```

Single dependency: HtmlAgilityPack.
