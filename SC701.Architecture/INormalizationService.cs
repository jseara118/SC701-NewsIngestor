using SC701.Models.DTOs;

namespace SC701.Architecture
{
    /// <summary>
    /// Servicio para normalizar contenido crudo al formato estándar acordado
    /// HU-18: Normalizar contenido al formato acordado
    /// </summary>
    public interface INormalizationService
    {
        /// <summary>
        /// Normaliza contenido JSON crudo al formato estándar
        /// </summary>
        StandardNewsItemDto NormalizeFromJson(string jsonContent, string sourceId, string sourceName, string sourceUrl, string sourceType);
        
        /// <summary>
        /// Normaliza contenido XML crudo al formato estándar
        /// </summary>
        StandardNewsItemDto NormalizeFromXml(string xmlContent, string sourceId, string sourceName, string sourceUrl, string sourceType);
        
        /// <summary>
        /// Normaliza contenido HTML crudo al formato estándar
        /// </summary>
        StandardNewsItemDto NormalizeFromHtml(string htmlContent, string sourceId, string sourceName, string sourceUrl, string sourceType);
    }
}

