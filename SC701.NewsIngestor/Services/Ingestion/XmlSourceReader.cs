using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Services.Ingestion;

// ComponentType soportados: rss, xml, atom, feed (case-insensitive)
public class XmlSourceReader : ISourceReader
{
    private readonly HttpClient _http;

    public XmlSourceReader(IHttpClientFactory factory)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (NewsIngestor/1.0)");
    }

    public bool CanHandle(string componentType) =>
        componentType.Equals("rss", StringComparison.OrdinalIgnoreCase) ||
        componentType.Equals("xml", StringComparison.OrdinalIgnoreCase) ||
        componentType.Equals("atom", StringComparison.OrdinalIgnoreCase) ||
        componentType.Equals("feed", StringComparison.OrdinalIgnoreCase);

    public async Task<StandardNewsItemDto> ReadAsync(Source source, CancellationToken ct = default)
    {
        var items = await ReadManyAsync(source, ct);
        return items.FirstOrDefault() ?? EmptyItem(source);
    }

    public async Task<List<StandardNewsItemDto>> ReadManyAsync(Source source, CancellationToken ct = default)
    {
        var xml = await _http.GetStringAsync(source.Url, ct);

        // Sanitizar XML antes de parsear para evitar errores con feeds sucios
        xml = SanitizeXml(xml);

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            // Si el XML está muy malformado, devolver item básico con la URL
            return new List<StandardNewsItemDto> { EmptyItem(source) };
        }

        var root = doc.Root;
        if (root == null) return new List<StandardNewsItemDto> { EmptyItem(source) };

        // RSS
        if (root.Name.LocalName is "rss" or "RDF")
        {
            var channel = root.Element("channel") ?? root;
            return channel.Elements("item").Take(20).Select(e => ParseRss(e, source)).ToList();
        }

        // Atom
        if (root.Name.LocalName == "feed" || (root.Name.NamespaceName?.Contains("atom") ?? false))
        {
            return root.Elements()
                .Where(e => e.Name.LocalName == "entry")
                .Take(20)
                .Select(e => ParseAtom(e, source))
                .ToList();
        }

        return new List<StandardNewsItemDto> { Build(source, source.Name, xml, null, DateTime.UtcNow, source.Url, null, null, source.DefaultCategory) };
    }

    /// <summary>
    /// Limpia el XML de problemas comunes: atributos sin comillas,
    /// entidades HTML no declaradas, caracteres de control.
    /// </summary>
    private static string SanitizeXml(string xml)
    {
        // Remover caracteres de control excepto tab, LF, CR
        xml = Regex.Replace(xml, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // Reemplazar entidades HTML comunes que no son válidas en XML
        xml = xml
            .Replace("&nbsp;", "&#160;")
            .Replace("&copy;", "&#169;")
            .Replace("&reg;", "&#174;")
            .Replace("&trade;", "&#8482;")
            .Replace("&mdash;", "&#8212;")
            .Replace("&ndash;", "&#8211;")
            .Replace("&laquo;", "&#171;")
            .Replace("&raquo;", "&#187;")
            .Replace("&ldquo;", "&#8220;")
            .Replace("&rdquo;", "&#8221;")
            .Replace("&lsquo;", "&#8216;")
            .Replace("&rsquo;", "&#8217;")
            .Replace("&hellip;", "&#8230;")
            .Replace("&bull;", "&#8226;")
            .Replace("&euro;", "&#8364;")
            .Replace("&pound;", "&#163;")
            .Replace("&yen;", "&#165;")
            .Replace("&cent;", "&#162;")
            .Replace("&acute;", "&#180;")
            .Replace("&iexcl;", "&#161;")
            .Replace("&iquest;", "&#191;");

        return xml;
    }

    private static StandardNewsItemDto ParseRss(XElement item, Source source)
    {
        var title = SafeText(item.Element("title")?.Value) ?? source.Name;
        var desc = SafeText(item.Element("description")?.Value) ?? "";
        var link = SafeText(item.Element("link")?.Value) ?? source.Url;
        var author = SafeText(item.Element("author")?.Value)
                    ?? SafeText(item.Elements().FirstOrDefault(e => e.Name.LocalName == "creator")?.Value);
        var pubDate = TryParseDate(item.Element("pubDate")?.Value) ?? DateTime.UtcNow;
        var category = SafeText(item.Element("category")?.Value)
                    ?? SafeText(item.Elements().FirstOrDefault(e => e.Name.LocalName == "category")?.Value)
                    ?? source.DefaultCategory;

        return Build(source, title, desc, desc, pubDate, link, author, item.Element("guid")?.Value ?? link, category);
    }

    private static StandardNewsItemDto ParseAtom(XElement entry, Source source)
    {
        var title = SafeText(entry.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value) ?? source.Name;
        var summary = SafeText(entry.Elements().FirstOrDefault(e => e.Name.LocalName == "summary")?.Value) ?? "";
        var content = SafeText(entry.Elements().FirstOrDefault(e => e.Name.LocalName == "content")?.Value) ?? summary;
        var link = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "link" && e.Attribute("rel")?.Value != "self")
                          ?.Attribute("href")?.Value ?? source.Url;
        var author = SafeText(entry.Elements().FirstOrDefault(e => e.Name.LocalName == "author")
                          ?.Elements().FirstOrDefault(e => e.Name.LocalName == "name")?.Value);
        var pubDate = TryParseDate(
                          entry.Elements().FirstOrDefault(e => e.Name.LocalName == "published")?.Value
                       ?? entry.Elements().FirstOrDefault(e => e.Name.LocalName == "updated")?.Value) ?? DateTime.UtcNow;
        var category = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "category")?.Attribute("term")?.Value
                    ?? source.DefaultCategory;

        return Build(source, title, content, summary, pubDate, link, author,
                     entry.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value, category);
    }

    private static StandardNewsItemDto Build(Source source, string title, string content, string? summary,
        DateTime publishedAt, string url, string? author, string? externalId, string? category)
    {
        // Strip HTML tags del content/summary para el resumen de card
        var cleanContent = StripHtml(content);
        var s = summary != null ? StripHtml(summary) : (cleanContent.Length > 220 ? cleanContent[..220] + "..." : cleanContent);

        return new StandardNewsItemDto
        {
            SchemaVersion = "edu.univ.ingest.v1",
            ExportedAt = DateTime.UtcNow,
            Source = new SourceDto
            {
                Id = source.Id.ToString(),
                Name = source.Name,
                Type = source.ComponentType,
                Url = source.Url,
                RequiresSecret = source.RequiresSecret
            },
            Normalized = new NormalizedContentDto
            {
                ExternalId = externalId,
                Title = title,
                Content = content,
                Summary = s,
                PublishedAt = publishedAt,
                Url = url,
                Author = author,
                Language = "es",
                Category = string.IsNullOrWhiteSpace(category) ? null : new CategoryDto { Primary = category.ToUpper() }
            },
            Raw = new RawDataDto { Format = "xml" }
        };
    }

    private static StandardNewsItemDto EmptyItem(Source source) =>
        Build(source, source.Name, "Sin contenido", null, DateTime.UtcNow, source.Url, null, null, source.DefaultCategory);

    private static string? SafeText(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string StripHtml(string s) =>
        string.IsNullOrWhiteSpace(s) ? "" : Regex.Replace(s, "<[^>]+>", " ").Trim();

    private static DateTime? TryParseDate(string? s) =>
        DateTime.TryParse(s, out var dt) ? dt.ToUniversalTime() : null;
}