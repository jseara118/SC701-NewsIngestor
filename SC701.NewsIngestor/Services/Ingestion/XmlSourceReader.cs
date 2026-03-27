using System.Xml.Linq;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Services.Ingestion;

// ComponentType soportados: rss, xml, atom, feed, RSS (case-insensitive)
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
        componentType.Equals("rss",  StringComparison.OrdinalIgnoreCase) ||
        componentType.Equals("xml",  StringComparison.OrdinalIgnoreCase) ||
        componentType.Equals("atom", StringComparison.OrdinalIgnoreCase) ||
        componentType.Equals("feed", StringComparison.OrdinalIgnoreCase);

    // Devuelve el primer item (cumple con ISourceReader)
    public async Task<StandardNewsItemDto> ReadAsync(Source source, CancellationToken ct = default)
    {
        var items = await ReadManyAsync(source, ct);
        return items.FirstOrDefault() ?? EmptyItem(source);
    }

    // Devuelve todos los items del feed
    public async Task<List<StandardNewsItemDto>> ReadManyAsync(Source source, CancellationToken ct = default)
    {
        var xml = await _http.GetStringAsync(source.Url, ct);
        var doc = XDocument.Parse(xml);
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

        return new List<StandardNewsItemDto> { Build(source, source.Name, xml, null, DateTime.UtcNow, source.Url, null, null) };
    }

    private static StandardNewsItemDto ParseRss(XElement item, Source source)
    {
        var title   = item.Element("title")?.Value?.Trim() ?? source.Name;
        var desc    = item.Element("description")?.Value?.Trim() ?? "";
        var link    = item.Element("link")?.Value?.Trim() ?? source.Url;
        var author  = item.Element("author")?.Value?.Trim()
                   ?? item.Elements().FirstOrDefault(e => e.Name.LocalName == "creator")?.Value?.Trim();
        var pubDate = TryParseDate(item.Element("pubDate")?.Value) ?? DateTime.UtcNow;
        return Build(source, title, desc, desc, pubDate, link, author, item.Element("guid")?.Value ?? link);
    }

    private static StandardNewsItemDto ParseAtom(XElement entry, Source source)
    {
        var title   = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value?.Trim()   ?? source.Name;
        var summary = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "summary")?.Value?.Trim() ?? "";
        var content = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "content")?.Value?.Trim() ?? summary;
        var link    = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "link" && e.Attribute("rel")?.Value != "self")
                          ?.Attribute("href")?.Value ?? source.Url;
        var author  = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "author")
                          ?.Elements().FirstOrDefault(e => e.Name.LocalName == "name")?.Value?.Trim();
        var pubDate = TryParseDate(
                          entry.Elements().FirstOrDefault(e => e.Name.LocalName == "published")?.Value
                       ?? entry.Elements().FirstOrDefault(e => e.Name.LocalName == "updated")?.Value) ?? DateTime.UtcNow;
        return Build(source, title, content, summary, pubDate, link, author,
                     entry.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value);
    }

    private static StandardNewsItemDto Build(Source source, string title, string content, string? summary,
        DateTime publishedAt, string url, string? author, string? externalId)
    {
        var s = summary ?? (content.Length > 220 ? content[..220] + "..." : content);
        return new StandardNewsItemDto
        {
            SchemaVersion = "edu.univ.ingest.v1",
            ExportedAt    = DateTime.UtcNow,
            Source = new SourceDto
            {
                Id = source.Id.ToString(), Name = source.Name,
                Type = source.ComponentType, Url = source.Url,
                RequiresSecret = source.RequiresSecret
            },
            Normalized = new NormalizedContentDto
            {
                ExternalId = externalId, Title = title, Content = content,
                Summary = s, PublishedAt = publishedAt, Url = url, Author = author, Language = "es"
            },
            Raw = new RawDataDto { Format = "xml" }
        };
    }

    private static StandardNewsItemDto EmptyItem(Source source) =>
        Build(source, source.Name, "Sin contenido", null, DateTime.UtcNow, source.Url, null, null);

    private static DateTime? TryParseDate(string? s) =>
        DateTime.TryParse(s, out var dt) ? dt.ToUniversalTime() : null;
}
