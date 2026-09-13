using System.Xml.Linq;

namespace BlogMlToWxr.Reading;

/// <summary>
/// BlogEngine.NET exports are not always namespace consistent between versions,
/// so every lookup here is done by local name.
/// </summary>
internal static class XmlExtensions
{
    public static XElement? Child(this XElement element, string localName) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<XElement> Children(this XElement element, string localName) =>
        element.Elements().Where(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    public static string Attr(this XElement element, string name) =>
        element.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

    public static string ChildValue(this XElement element, string localName) =>
        element.Child(localName)?.Value?.Trim() ?? string.Empty;

    public static DateTime AttrDate(this XElement element, string name, DateTime fallback)
    {
        var raw = element.Attr(name);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;

        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    public static bool AttrBool(this XElement element, string name, bool fallback)
    {
        var raw = element.Attr(name);
        return bool.TryParse(raw, out var parsed) ? parsed : fallback;
    }
}
