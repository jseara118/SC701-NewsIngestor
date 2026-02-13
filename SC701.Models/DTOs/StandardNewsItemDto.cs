using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

// DTOs basados en el formato JSON acordado por todos los grupos
// Template: edu.univ.ingest.v1

namespace SC701.Models.DTOs
{
    /// <summary>
    /// Estructura completa del JSON estándar acordado (templateV2.json)
    /// </summary>
    public class StandardNewsItemDto
    {
        [Required]
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = "edu.univ.ingest.v1";

        [Required]
        [JsonPropertyName("exportedAt")]
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [JsonPropertyName("source")]
        public SourceDto Source { get; set; } = null!;

        [Required]
        [JsonPropertyName("normalized")]
        public NormalizedContentDto Normalized { get; set; } = null!;

        [JsonPropertyName("raw")]
        public RawDataDto? Raw { get; set; }
    }

    /// <summary>
    /// Información de la fuente del contenido
    /// </summary>
    public class SourceDto
    {
        /// <summary>
        /// ID de la fuente (puede usarse para agrupar múltiples artículos)
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [Required]
        [MaxLength(200)]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty; // "api", "feed", "html"

        [Required]
        [Url]
        [MaxLength(500)]
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("requiresSecret")]
        public bool RequiresSecret { get; set; }
    }

    /// <summary>
    /// Contenido normalizado del artículo
    /// </summary>
    public class NormalizedContentDto
    {
        /// <summary>
        /// Identidad única del contenido en el sistema de normalización
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>
        /// ID del contenido en la fuente original
        /// </summary>
        [JsonPropertyName("externalId")]
        public string? ExternalId { get; set; }

        [Required]
        [MaxLength(500)]
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [Required]
        [JsonPropertyName("publishedAt")]
        public DateTime PublishedAt { get; set; }

        [Url]
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [MaxLength(10)]
        [JsonPropertyName("language")]
        public string? Language { get; set; } = "es";

        [JsonPropertyName("category")]
        public CategoryDto? Category { get; set; }
    }

    /// <summary>
    /// Categorización del contenido
    /// </summary>
    public class CategoryDto
    {
        [JsonPropertyName("primary")]
        public string? Primary { get; set; }

        [JsonPropertyName("secondary")]
        public List<string>? Secondary { get; set; }
    }

    /// <summary>
    /// Datos originales sin procesar (raw)
    /// </summary>
    public class RawDataDto
    {
        [JsonPropertyName("format")]
        public string Format { get; set; } = "json"; // "json", "xml", "html"

        [JsonPropertyName("data")]
        public object? Data { get; set; }
    }
}