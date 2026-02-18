using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.Architecture
{
    /// <summary>
    /// Interfaz base para leer contenido de fuentes de noticias
    /// HU-18: Normalizar contenido al formato acordado
    /// </summary>
    public interface ISourceReader
    {
        /// <summary>
        /// Lee contenido desde una fuente y retorna items normalizados
        /// </summary>
        Task<List<StandardNewsItemDto>> ReadFromSourceAsync(Source source, string? secretValue = null);
        
        /// <summary>
        /// Verifica si este reader puede procesar el tipo de fuente especificado
        /// </summary>
        bool CanHandle(string componentType);
    }
}

