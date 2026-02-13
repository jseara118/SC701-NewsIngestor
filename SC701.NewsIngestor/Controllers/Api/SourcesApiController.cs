using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;

namespace SC701.NewsIngestor.Controllers.Api
{
    /// <summary>
    /// API Controller para gestionar Sources (Fuentes de noticias)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SourcesApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SourcesApiController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Obtiene todas las fuentes registradas
        /// </summary>
        /// <returns>Lista de todas las fuentes</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Source>>> GetSources()
        {
            var sources = await _context.Sources
                .Include(s => s.SourceItems)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return Ok(sources);
        }

        /// <summary>
        /// Obtiene una fuente específica por su ID
        /// </summary>
        /// <param name="id">ID de la fuente</param>
        /// <returns>La fuente solicitada</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Source>> GetSource(int id)
        {
            var source = await _context.Sources
                .Include(s => s.SourceItems)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (source == null)
            {
                return NotFound(new { message = $"No se encontró la fuente con ID {id}" });
            }

            return Ok(source);
        }

        /// <summary>
        /// Crea una nueva fuente
        /// </summary>
        /// <param name="source">Datos de la fuente a crear</param>
        /// <returns>La fuente creada</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Source>> CreateSource([FromBody] Source source)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verificar si ya existe una fuente con la misma URL
            var existingSource = await _context.Sources
                .FirstOrDefaultAsync(s => s.Url == source.Url);

            if (existingSource != null)
            {
                return BadRequest(new { message = "Ya existe una fuente con esta URL" });
            }

            _context.Sources.Add(source);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetSource),
                new { id = source.Id },
                source
            );
        }

        /// <summary>
        /// Actualiza una fuente existente
        /// </summary>
        /// <param name="id">ID de la fuente a actualizar</param>
        /// <param name="source">Datos actualizados de la fuente</param>
        /// <returns>Sin contenido si fue exitoso</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSource(int id, [FromBody] Source source)
        {
            if (id != source.Id)
            {
                return BadRequest(new { message = "El ID de la URL no coincide con el ID del objeto" });
            }

            var existingSource = await _context.Sources.FindAsync(id);
            if (existingSource == null)
            {
                return NotFound(new { message = $"No se encontró la fuente con ID {id}" });
            }

            // Actualizar propiedades
            existingSource.Url = source.Url;
            existingSource.Name = source.Name;
            existingSource.Description = source.Description;
            existingSource.ComponentType = source.ComponentType;
            existingSource.RequiresSecret = source.RequiresSecret;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await SourceExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        /// <summary>
        /// Elimina una fuente
        /// </summary>
        /// <param name="id">ID de la fuente a eliminar</param>
        /// <returns>Sin contenido si fue exitoso</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteSource(int id)
        {
            var source = await _context.Sources
                .Include(s => s.SourceItems)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (source == null)
            {
                return NotFound(new { message = $"No se encontró la fuente con ID {id}" });
            }

            // Verificar si tiene items asociados
            if (source.SourceItems != null && source.SourceItems.Any())
            {
                return BadRequest(new
                {
                    message = "No se puede eliminar la fuente porque tiene items asociados",
                    itemCount = source.SourceItems.Count
                });
            }

            _context.Sources.Remove(source);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> SourceExists(int id)
        {
            return await _context.Sources.AnyAsync(e => e.Id == id);
        }
    }
}