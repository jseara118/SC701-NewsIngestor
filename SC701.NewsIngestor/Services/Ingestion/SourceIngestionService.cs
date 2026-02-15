using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Services.Ingestion;

public class SourceIngestionService : ISourceIngestionService
{
    private readonly IEnumerable<ISourceReader> _readers;

    public SourceIngestionService(IEnumerable<ISourceReader> readers)
    {
        _readers = readers;
    }

    public Task<StandardNewsItemDto> IngestAsync(Source source, CancellationToken ct = default)
    {
        var reader = _readers.FirstOrDefault(r => r.CanHandle(source.ComponentType));
        if (reader == null)
            throw new InvalidOperationException($"No hay lector para ComponentType='{source.ComponentType}'");

        return reader.ReadAsync(source, ct);
    }
}
