using HtmlAgilityPack;
using SC701.Models;
using SC701.Models.DTOs;
using System.Text.RegularExpressions;

namespace SC701.NewsIngestor.Services.Ingestion;

public class HtmlSourceReader : ISourceReader
{
    private readonly HttpClient _http;

    public HtmlSourceReader(IHttpClientFactory factory)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (NewsIngestor/1.0)");
    }

    public bool CanHandle(string componentType)
        => componentType.Equals("html", StringComparison.OrdinalIgnoreCase)
        || componentType.Equals("api", StringComparison.OrdinalIgnoreCase)
        || componentType.Equals("WebScraper", StringComparison.OrdinalIgnoreCase);

    public async Task<StandardNewsItemDto> ReadAsync(Source source, CancellationToken ct = default)
        => await ScrapeUrl(source, source.Url, ct);

    public async Task<List<StandardNewsItemDto>> ReadManyAsync(Source source, CancellationToken ct = default)
    {
        var urls = source.GetAllUrls();
        var results = new List<StandardNewsItemDto>();

        foreach (var url in urls)
        {
            try { results.Add(await ScrapeUrl(source, url, ct)); }
            catch { /* si una URL falla, continuamos con las demás */ }
        }

        return results;
    }

    private async Task<StandardNewsItemDto> ScrapeUrl(Source source, string url, CancellationToken ct)
    {
        var html = await _http.GetStringAsync(url, ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style") ?? Enumerable.Empty<HtmlNode>())
            node.Remove();

        string title =
            Meta(doc, "property", "og:title")
            ?? Meta(doc, "name", "twitter:title")
            ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText
            ?? doc.DocumentNode.SelectSingleNode("//h1")?.InnerText
            ?? source.Name;

        string content =
            Meta(doc, "property", "og:description")
            ?? Meta(doc, "name", "description")
            ?? ExtractMainText(doc);

        content = CleanText(content);
        title = CleanText(title);

        DateTime publishedAt =
            TryParseDate(Meta(doc, "property", "article:published_time"))
            ?? TryParseDate(Meta(doc, "name", "pubdate"))
            ?? TryParseDate(Meta(doc, "itemprop", "datePublished"))
            ?? DateTime.UtcNow;

        string? author =
            Meta(doc, "name", "author")
            ?? Meta(doc, "property", "article:author");

        // Categoría: primero la del meta tag, luego el DefaultCategory de la fuente
        string? category =
            Meta(doc, "property", "article:section")
            ?? Meta(doc, "name", "category")
            ?? source.DefaultCategory;

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
                Title = title,
                Content = content,
                Summary = content.Length > 220 ? content[..220] + "..." : content,
                PublishedAt = publishedAt,
                Url = url,
                Author = author,
                Language = "es",
                Category = string.IsNullOrWhiteSpace(category) ? null : new CategoryDto { Primary = category }
            },
            Raw = new RawDataDto { Format = "html", Data = new { sourceUrl = url } }
        };
    }

    private static string? Meta(HtmlDocument doc, string attrName, string attrValue)
    {
        var node = doc.DocumentNode.SelectSingleNode($"//meta[@{attrName}='{attrValue}']");
        return node?.GetAttributeValue("content", null);
    }

    private static string ExtractMainText(HtmlDocument doc)
    {
        var main =
            doc.DocumentNode.SelectSingleNode("//article")
            ?? doc.DocumentNode.SelectSingleNode("//main")
            ?? doc.DocumentNode.SelectSingleNode("//body")
            ?? doc.DocumentNode;
        return HtmlEntity.DeEntitize(main.InnerText ?? "");
    }

    private static string CleanText(string s)
        => Regex.Replace((s ?? "").Trim(), @"\s+", " ");

    private static DateTime? TryParseDate(string? s)
        => DateTime.TryParse(s, out var dt) ? dt.ToUniversalTime() : null;
}