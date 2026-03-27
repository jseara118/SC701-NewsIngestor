using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Services.Ingestion;

public interface ISourceReader
{
    bool CanHandle(string componentType);
    Task<StandardNewsItemDto> ReadAsync(Source source, CancellationToken ct = default);
}
