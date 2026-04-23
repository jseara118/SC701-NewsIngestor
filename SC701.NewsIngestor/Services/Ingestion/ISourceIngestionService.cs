using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Services.Ingestion;

public interface ISourceIngestionService
{
    Task<StandardNewsItemDto> IngestAsync(Source source, CancellationToken ct = default);
    Task<List<StandardNewsItemDto>> IngestManyAsync(Source source, CancellationToken ct = default);
}
