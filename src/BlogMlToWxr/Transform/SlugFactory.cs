using System.Globalization;
using System.Text;

namespace BlogMlToWxr.Transform;

/// <summary>
/// Produces WordPress friendly slugs and guarantees uniqueness across the whole export.
/// </summary>
public sealed class SlugFactory
{
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

    public string Create(string input)
    {
        var slug = Normalize(input);
        if (string.IsNullOrWhiteSpace(slug)) slug = "post";

        if (_used.Add(slug)) return slug;

        var counter = 2;
        string candidate;
        do
        {
            candidate = $"{slug}-{counter}";
            counter++;
        }
        while (!_used.Add(candidate));

        return candidate;
    }

    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var trimmed = input.Trim();

        // A post-name coming from BlogEngine is often already a slug or an .aspx file name.
        if (trimmed.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^5];

        var decomposed = trimmed.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append('-');
            }
        }

        var raw = builder.ToString().Normalize(NormalizationForm.FormC);

        while (raw.Contains("--", StringComparison.Ordinal))
            raw = raw.Replace("--", "-", StringComparison.Ordinal);

        return raw.Trim('-');
    }
}
