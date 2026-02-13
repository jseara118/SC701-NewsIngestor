using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;

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

        /// <summary>
        /// Obtiene todos los items de noticias
        /// </summary>
        /// <returns>Lista de todos los items</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<SourceItem>>> GetSourceItems()
        {
            var items = await _context.SourceItems
                .Include(i => i.Source)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>
        /// Obtiene un item específico por su ID
        /// </summary>
        /// <param name="id">ID del item</param>
        /// <returns>El item solicitado</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SourceItem>> GetSourceItem(int id)
        {
            var item = await _context.SourceItems
                .Include(i => i.Source)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return NotFound(new { message = $"No se encontró el item con ID {id}" });
            }

            return Ok(item);
        }

        /// <summary>
        /// Obtiene todos los items de una fuente específica
        /// </summary>
        /// <param name="sourceId">ID de la fuente</param>
        /// <returns>Lista de items de la fuente</returns>
        [HttpGet("source/{sourceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<SourceItem>>> GetItemsBySource(int sourceId)
        {
            var source = await _context.Sources.FindAsync(sourceId);
            if (source == null)
            {
                return NotFound(new { message = $"No se encontró la fuente con ID {sourceId}" });
            }

            var items = await _context.SourceItems
                .Include(i => i.Source)
                .Where(i => i.SourceId == sourceId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>
        /// Crea un nuevo item de noticia
        /// </summary>
        /// <param name="item">Datos del item a crear</param>
        /// <returns>El item creado</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SourceItem>> CreateSourceItem([FromBody] SourceItem item)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verificar que la fuente existe
            var source = await _context.Sources.FindAsync(item.SourceId);
            if (source == null)
            {
                return BadRequest(new { message = "La fuente especificada no existe" });
            }

            // Verificar duplicados (mismo SourceId y mismo JSON)
            var existingItem = await _context.SourceItems
                .FirstOrDefaultAsync(i => i.SourceId == item.SourceId && i.Json == item.Json);

            if (existingItem != null)
            {
                return BadRequest(new { message = "Ya existe un item idéntico para esta fuente" });
            }

            // Asignar fecha de creación si no viene
            if (item.CreatedAt == default)
            {
                item.CreatedAt = DateTime.UtcNow;
            }

            _context.SourceItems.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetSourceItem),
                new { id = item.Id },
                item
            );
        }

        /// <summary>
        /// Actualiza un item existente
        /// </summary>
        /// <param name="id">ID del item a actualizar</param>
        /// <param name="item">Datos actualizados del item</param>
        /// <returns>Sin contenido si fue exitoso</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSourceItem(int id, [FromBody] SourceItem item)
        {
            if (id != item.Id)
            {
                return BadRequest(new { message = "El ID de la URL no coincide con el ID del objeto" });
            }

            var existingItem = await _context.SourceItems.FindAsync(id);
            if (existingItem == null)
            {
                return NotFound(new { message = $"No se encontró el item con ID {id}" });
            }

            // Verificar que la fuente existe
            var source = await _context.Sources.FindAsync(item.SourceId);
            if (source == null)
            {
                return BadRequest(new { message = "La fuente especificada no existe" });
            }

            // Actualizar propiedades
            existingItem.SourceId = item.SourceId;
            existingItem.Json = item.Json;
            // No actualizamos CreatedAt

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await SourceItemExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        /// <summary>
        /// Elimina un item
        /// </summary>
        /// <param name="id">ID del item a eliminar</param>
        /// <returns>Sin contenido si fue exitoso</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSourceItem(int id)
        {
            var item = await _context.SourceItems.FindAsync(id);
            if (item == null)
            {
                return NotFound(new { message = $"No se encontró el item con ID {id}" });
            }

            _context.SourceItems.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> SourceItemExists(int id)
        {
            return await _context.SourceItems.AnyAsync(e => e.Id == id);
        }
    }
}