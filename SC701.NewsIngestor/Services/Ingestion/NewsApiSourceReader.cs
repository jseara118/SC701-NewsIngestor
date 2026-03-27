using System.Text.Json;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Services.Ingestion;

// ComponentType: "newsapi"
// Requiere Secret con Name="apiKey" ligado a la fuente.
// URL ejemplo: https://newsapi.org/v2/top-headlines?country=us
public class NewsApiSourceReader : ISourceReader
{
    private readonly HttpClient _http;

    public NewsApiSourceReader(IHttpClientFactory factory)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public bool CanHandle(string componentType) =>
        componentType.Equals("newsapi", StringComparison.OrdinalIgnoreCase) ||
        componentType.Equals("api", StringComparison.OrdinalIgnoreCase);

    // Cumple con ISourceReader — necesita el apiKey via SourceIngestionService
    public Task<StandardNewsItemDto> ReadAsync(Source source, CancellationToken ct = default)
        => Task.FromResult(EmptyItem(source)); // sin key no se puede — SourceIngestionService llama ReadManyAsync

    public async Task<List<StandardNewsItemDto>> ReadManyAsync(Source source, string? apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                $"La fuente '{source.Name}' requiere un Secret. " +
                "Agregalo en Configuración → Secrets (Name=apiKey).");

        var url = source.Url.Contains("apiKey=", StringComparison.OrdinalIgnoreCase)
            ? source.Url
            : $"{source.Url}{(source.Url.Contains('?') ? "&" : "?")}apiKey={apiKey}";

        var json = await _http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("articles", out var articles))
            throw new InvalidOperationException(
                $"NewsAPI no devolvió 'articles'. Respuesta: {json[..Math.Min(200, json.Length)]}");

        var result = new List<StandardNewsItemDto>();
        foreach (var a in articles.EnumerateArray().Take(20))
        {
            var title = a.Str("title") ?? source.Name;
            var desc = a.Str("description") ?? "";
            var content = a.Str("content") ?? desc;
            var link = a.Str("url") ?? source.Url;
            var author = a.Str("author");
            var pub = DateTime.TryParse(a.Str("publishedAt"), out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow;

            if (content.Contains("[+")) content = content[..content.LastIndexOf('[')].Trim();

            result.Add(new StandardNewsItemDto
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
                    Summary = desc,
                    PublishedAt = pub,
                    Url = link,
                    Author = author,
                    Language = "en"
                },
                Raw = new RawDataDto { Format = "json" }
            });
        }
        return result;
    }

    private static StandardNewsItemDto EmptyItem(Source source) => new()
    {
        SchemaVersion = "edu.univ.ingest.v1",
        ExportedAt = DateTime.UtcNow,
        Source = new SourceDto { Name = source.Name, Type = source.ComponentType, Url = source.Url },
        Normalized = new NormalizedContentDto { Title = source.Name, Content = "Sin contenido", PublishedAt = DateTime.UtcNow }
    };
}

internal static class JsonElementExt
{
    public static string? Str(this JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
