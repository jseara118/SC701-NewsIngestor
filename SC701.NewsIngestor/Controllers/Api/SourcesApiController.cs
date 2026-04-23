using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;
using SC701.Models.DTOs;
using SC701.NewsIngestor.Services.Ingestion;
using System.Text.Json;

namespace SC701.NewsIngestor.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class SourcesApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ISourceIngestionService _ingestion;

    public SourcesApiController(AppDbContext context, ISourceIngestionService ingestion)
    {
        _context = context;
        _ingestion = ingestion;
    }

    // GET: api/Sources
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Source>>> GetSources()
    {
        var sources = await _context.Sources
            .Include(s => s.SourceItems)
            .OrderBy(s => s.Name)
            .ToListAsync();
        return Ok(sources);
    }

    // GET: api/Sources/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Source>> GetSource(int id)
    {
        var source = await _context.Sources
            .Include(s => s.SourceItems)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (source == null)
            return NotFound(new { message = $"No se encontró la fuente con ID {id}" });

        return Ok(source);
    }

    // GET: api/Sources/5/preview
    [HttpGet("{id:int}/preview")]
    public async Task<IActionResult> PreviewSource(int id)
    {
        var source = await _context.Sources.FindAsync(id);
        if (source == null)
            return NotFound(new { message = $"No se encontró la fuente con ID {id}" });

        try
        {
            var items = await _ingestion.IngestManyAsync(source);

            var preview = items.Select(dto => new
            {
                title = dto.Normalized?.Title,
                summary = dto.Normalized?.Summary,
                content = dto.Normalized?.Content,
                author = dto.Normalized?.Author,
                publishedAt = dto.Normalized?.PublishedAt,
                articleUrl = dto.Normalized?.Url,
                category = dto.Normalized?.Category?.Primary,
                language = dto.Normalized?.Language,
                sourceName = dto.Source?.Name,
                sourceType = dto.Source?.Type,
                rawJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                })
            }).ToList();

            return Ok(new
            {
                sourceId = source.Id,
                sourceName = source.Name,
                count = preview.Count,
                items = preview
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST: api/Sources/5/save-item
    [HttpPost("{id:int}/save-item")]
    [Authorize]
    public async Task<IActionResult> SavePreviewItem(int id, [FromBody] SaveItemRequest req)
    {
        var source = await _context.Sources.FindAsync(id);
        if (source == null)
            return NotFound(new { message = $"No se encontró la fuente con ID {id}" });

        if (string.IsNullOrWhiteSpace(req.RawJson))
            return BadRequest(new { message = "rawJson es requerido" });

        // Extraer título y URL del JSON para detección de duplicados más robusta
        string? title = null;
        string? url = null;
        string? normalizedId = null;

        try
        {
            var dto = JsonSerializer.Deserialize<StandardNewsItemDto>(req.RawJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            title = dto?.Normalized?.Title?.Trim();
            url = dto?.Normalized?.Url?.Trim();
            normalizedId = dto?.Normalized?.Id ?? dto?.Normalized?.ExternalId;
        }
        catch { /* si no parsea, caemos al check por JSON exacto */ }

        // Check 1: por NormalizedId si existe
        if (!string.IsNullOrWhiteSpace(normalizedId))
        {
            var existsById = await _context.SourceItems
                .AnyAsync(i => i.NormalizedId == normalizedId);
            if (existsById)
                return Conflict(new { message = "Esta noticia ya está guardada (ID duplicado)" });
        }

        // Check 2: por título + fuente (evita duplicados de Halo Waypoint sin ID)
        if (!string.IsNullOrWhiteSpace(title))
        {
            var existsByTitle = await _context.SourceItems
                .Where(i => i.SourceId == id)
                .AnyAsync(i => i.Json.Contains(title));
            if (existsByTitle)
                return Conflict(new { message = $"Ya existe una noticia con el título \"{title}\"" });
        }

        // Check 3: fallback — JSON exacto
        var existsExact = await _context.SourceItems
            .AnyAsync(i => i.SourceId == id && i.Json == req.RawJson);
        if (existsExact)
            return Conflict(new { message = "Este item ya está guardado" });

        var item = new SourceItem
        {
            SourceId = id,
            Json = req.RawJson,
            NormalizedId = normalizedId,
            CreatedAt = DateTime.UtcNow
        };

        _context.SourceItems.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSource), new { id },
            new { message = "Item guardado", itemId = item.Id, sourceId = id });
    }

    // POST: api/Sources
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Source>> CreateSource([FromBody] Source source)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existing = await _context.Sources
            .FirstOrDefaultAsync(s => s.Url.ToLower() == source.Url.ToLower().TrimEnd('/'));
        if (existing != null)
            return BadRequest(new { message = "Ya existe una fuente con esta URL" });

        _context.Sources.Add(source);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSource), new { id = source.Id }, source);
    }

    // DELETE: api/Sources/5
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSource(int id)
    {
        var source = await _context.Sources
            .Include(s => s.SourceItems)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (source == null)
            return NotFound(new { message = $"No se encontró la fuente con ID {id}" });

        if (source.SourceItems?.Any() == true)
            return BadRequest(new
            {
                message = "No se puede eliminar la fuente porque tiene items asociados",
                itemCount = source.SourceItems.Count
            });

        _context.Sources.Remove(source);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class SaveItemRequest
{
    public string RawJson { get; set; } = string.Empty;
}