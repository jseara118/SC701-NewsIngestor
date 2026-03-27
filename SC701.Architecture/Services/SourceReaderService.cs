using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.Architecture.Services
{
    /// <summary>
    /// Servicio orquestador para leer contenido de diferentes fuentes
    /// HU-18: Normalizar contenido al formato acordado
    /// </summary>
    public class SourceReaderService
    {
        private readonly IEnumerable<ISourceReader> _readers;
        private readonly AppDbContext _context;

        public SourceReaderService(IEnumerable<ISourceReader> readers, AppDbContext context)
        {
            _readers = readers;
            _context = context;
        }

        /// <summary>
        /// Lee contenido de una fuente específica
        /// </summary>
        public async Task<List<StandardNewsItemDto>> ReadFromSourceAsync(Source source)
        {
            var reader = _readers.FirstOrDefault(r => r.CanHandle(source.ComponentType));
            if (reader == null)
            {
                throw new NotSupportedException($"No se encontró un reader para el tipo de componente: {source.ComponentType}");
            }

            // Obtener secret si es necesario
            string? secretValue = null;
            if (source.RequiresSecret)
            {
                var secret = await _context.Secrets
                    .FirstOrDefaultAsync(s => s.SourceId == source.Id);
                secretValue = secret?.Value;
            }

            return await reader.ReadFromSourceAsync(source, secretValue);
        }

        /// <summary>
        /// Lee contenido de todas las fuentes disponibles
        /// </summary>
        public async Task<List<StandardNewsItemDto>> ReadFromAllSourcesAsync()
        {
            var allItems = new List<StandardNewsItemDto>();
            var sources = await _context.Sources.ToListAsync();

            foreach (var source in sources)
            {
                try
                {
                    var items = await ReadFromSourceAsync(source);
                    allItems.AddRange(items);
                }
                catch (Exception ex)
                {
                    // Log error pero continuar con otras fuentes
                    Console.WriteLine($"Error procesando fuente {source.Name}: {ex.Message}");
                }
            }

            return allItems;
        }
    }
}

