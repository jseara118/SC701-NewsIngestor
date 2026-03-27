using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SC701.Data;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Controllers.Api
{
    /// <summary>
    /// API Controller para gestionar SourceItems (Items de noticias)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SourceItemsApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SourceItemsApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/SourceItems
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SourceItem>>> GetSourceItems()
        {
            var items = await _context.SourceItems
                .Include(i => i.Source)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
            return Ok(items);
        }

        // GET: api/SourceItems/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<SourceItem>> GetSourceItem(int id)
        {
            var item = await _context.SourceItems
                .Include(i => i.Source)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
                return NotFound(new { message = $"No se encontró el item con ID {id}" });

            return Ok(item);
        }

        // ─────────────────────────────────────────────────────────────────
        // GET: api/SourceItems/{id}/detail
        // Endpoint usado por el modal en Items/Index.cshtml.
        // Parsea el JSON del SourceItem y devuelve un objeto plano con
        // todos los campos ya extraídos, listo para llenar el modal.
        //
        // Respuesta exitosa:
        // {
        //   itemId, title, summary, content, author, publishedAt,
        //   articleUrl, category, language, sourceName, sourceType, createdAt
        // }
        // ─────────────────────────────────────────────────────────────────
        [HttpGet("{id}/detail")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDetail(int id)
        {
            var item = await _context.SourceItems
                .Include(i => i.Source)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
                return NotFound(new { message = $"No se encontró el item con ID {id}" });

            // Intentar parsear al formato estándar
            StandardNewsItemDto? dto = null;
            try
            {
                dto = JsonSerializer.Deserialize<StandardNewsItemDto>(item.Json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* fallback a campos raw */ }

            // Construir respuesta plana para el modal
            var detail = new
            {
                itemId = item.Id,
                title = dto?.Normalized?.Title ?? $"Noticia #{item.Id}",
                summary = dto?.Normalized?.Summary,
                content = dto?.Normalized?.Content,
                author = dto?.Normalized?.Author,
                publishedAt = dto?.Normalized?.PublishedAt ?? item.CreatedAt,
                articleUrl = dto?.Normalized?.Url,
                category = dto?.Normalized?.Category?.Primary,
                language = dto?.Normalized?.Language ?? "es",
                sourceName = dto?.Source?.Name ?? item.Source?.Name,
                sourceType = dto?.Source?.Type ?? item.Source?.ComponentType,
                createdAt = item.CreatedAt
            };

            return Ok(detail);
        }

        // GET: api/SourceItems/source/{sourceId}
        [HttpGet("source/{sourceId}")]
        public async Task<ActionResult<IEnumerable<SourceItem>>> GetItemsBySource(int sourceId)
        {
            var source = await _context.Sources.FindAsync(sourceId);
            if (source == null)
                return NotFound(new { message = $"No se encontró la fuente con ID {sourceId}" });

            var items = await _context.SourceItems
                .Include(i => i.Source)
                .Where(i => i.SourceId == sourceId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return Ok(items);
        }

        // POST: api/SourceItems
        [HttpPost]
        public async Task<ActionResult<SourceItem>> CreateSourceItem([FromBody] SourceItem item)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var source = await _context.Sources.FindAsync(item.SourceId);
            if (source == null)
                return BadRequest(new { message = "La fuente especificada no existe" });

            var existingItem = await _context.SourceItems
                .FirstOrDefaultAsync(i => i.SourceId == item.SourceId && i.Json == item.Json);
            if (existingItem != null)
                return BadRequest(new { message = "Ya existe un item idéntico para esta fuente" });

            if (item.CreatedAt == default) item.CreatedAt = DateTime.UtcNow;

            _context.SourceItems.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSourceItem), new { id = item.Id }, item);
        }

        // PUT: api/SourceItems/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSourceItem(int id, [FromBody] SourceItem item)
        {
            if (id != item.Id)
                return BadRequest(new { message = "El ID no coincide" });

            var existingItem = await _context.SourceItems.FindAsync(id);
            if (existingItem == null)
                return NotFound(new { message = $"No se encontró el item con ID {id}" });

            var source = await _context.Sources.FindAsync(item.SourceId);
            if (source == null)
                return BadRequest(new { message = "La fuente especificada no existe" });

            existingItem.SourceId = item.SourceId;
            existingItem.Json = item.Json;

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await SourceItemExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/SourceItems/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSourceItem(int id)
        {
            var item = await _context.SourceItems.FindAsync(id);
            if (item == null)
                return NotFound(new { message = $"No se encontró el item con ID {id}" });

            _context.SourceItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<bool> SourceItemExists(int id)
            => await _context.SourceItems.AnyAsync(e => e.Id == id);
    }
}
