using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Services.Ingestion;

public class SourceIngestionService : ISourceIngestionService
{
    private readonly IEnumerable<ISourceReader> _readers;
    private readonly AppDbContext _context;

    public SourceIngestionService(IEnumerable<ISourceReader> readers, AppDbContext context)
    {
        _readers = readers;
        _context = context;
    }

    public async Task<StandardNewsItemDto> IngestAsync(Source source, CancellationToken ct = default)
    {
        var items = await IngestManyAsync(source, ct);
        return items.FirstOrDefault()
            ?? throw new InvalidOperationException($"No se obtuvo ningún item de '{source.Name}'");
    }

    public async Task<List<StandardNewsItemDto>> IngestManyAsync(Source source, CancellationToken ct = default)
    {
        var reader = _readers.FirstOrDefault(r => r.CanHandle(source.ComponentType))
            ?? throw new InvalidOperationException(
                $"No hay lector para ComponentType='{source.ComponentType}'. " +
                "Válidos: json, api, rss, xml, atom, html, newsapi.");

        string? secretValue = null;
        if (source.RequiresSecret)
        {
            secretValue = await _context.Secrets
                .Where(s => s.SourceId == source.Id)
                .OrderByDescending(s => s.UpdatedAt)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(ct);
        }

        if (reader is NewsApiSourceReader newsApi)
            return await newsApi.ReadManyAsync(source, secretValue, ct);

        if (reader is XmlSourceReader xml)
            return await xml.ReadManyAsync(source, ct);

        var single = await reader.ReadAsync(source, ct);
        return new List<StandardNewsItemDto> { single };
    }
}
