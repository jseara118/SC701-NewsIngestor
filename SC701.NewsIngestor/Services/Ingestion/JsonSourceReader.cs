using System.Text.Json;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Services.Ingestion;

public class JsonSourceReader : ISourceReader
{
    private readonly HttpClient _http;

    public JsonSourceReader(IHttpClientFactory factory)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public bool CanHandle(string componentType)
     => componentType.Equals("json", StringComparison.OrdinalIgnoreCase);

    public async Task<StandardNewsItemDto> ReadAsync(Source source, CancellationToken ct = default)
    {
        var jsonText = await _http.GetStringAsync(source.Url, ct);

        // 1) Si ya viene en el estándar, lo dejamos tal cual.
        try
        {
            var standard = JsonSerializer.Deserialize<StandardNewsItemDto>(
                jsonText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (standard?.SchemaVersion == "edu.univ.ingest.v1" && standard.Source != null && standard.Normalized != null)
                return standard;
        }
        catch { /* cae al modo heurístico */ }

        // 2) Heurística: mapear campos típicos
        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        // si viene array, tomamos el primero
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            root = root[0];

        string? title = PickFirstString(root, "title", "headline", "name");
        string? content = PickFirstString(root, "content", "body", "description", "text");
        string? url = PickFirstString(root, "url", "link");
        string? author = PickFirstString(root, "author", "by", "writer");
        DateTime publishedAt = PickFirstDate(root, "publishedAt", "published_at", "date", "createdAt", "created_at") ?? DateTime.UtcNow;

        title ??= source.Name;
        content ??= jsonText;

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
                ExternalId = PickFirstString(root, "id", "externalId"),
                Title = title,
                Content = content,
                Summary = content.Length > 220 ? content[..220] + "..." : content,
                PublishedAt = publishedAt,
                Url = url ?? source.Url,
                Author = author,
                Language = "es"
            },
            Raw = new RawDataDto
            {
                Format = "json",
                Data = JsonSerializer.Deserialize<object>(jsonText)
            }
        };
    }

    private static string? PickFirstString(JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                if (v.ValueKind == JsonValueKind.Number) return v.ToString();
            }
        }
        return null;
    }

    private static DateTime? PickFirstDate(JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.String && DateTime.TryParse(v.GetString(), out var dt)) return dt.ToUniversalTime();
            }
        }
        return null;
    }
}
