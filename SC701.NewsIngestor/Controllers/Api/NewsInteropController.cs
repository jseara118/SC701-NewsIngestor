using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using SC701.Data;
using SC701.Models;
using SC701.Models.DTOs;

namespace SC701.NewsIngestor.Controllers.Api
{
    /// <summary>
    /// API Controller para importar/exportar noticias usando el formato estándar acordado
    /// Template: edu.univ.ingest.v1 (templateV2.json)
    /// Implementa HU-25 (Download), HU-26 (Upload), HU-27 (Validación), HU-28 (Auto-crear Source)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class NewsInteropController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NewsInteropController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Exporta un SourceItem en formato JSON estándar acordado (HU-25)
        /// </summary>
        /// <param name="id">ID del SourceItem a exportar</param>
        /// <returns>Archivo JSON descargable en formato edu.univ.ingest.v1</returns>
        [HttpGet("export/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ExportItem(int id)
        {
            var item = await _context.SourceItems
                .Include(i => i.Source)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return NotFound(new { message = $"No se encontró el item con ID {id}" });
            }

            StandardNewsItemDto standardItem;

            try
            {
                // Intentar deserializar si ya está en formato estándar
                standardItem = JsonSerializer.Deserialize<StandardNewsItemDto>(
                    item.Json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? throw new Exception("Deserialización retornó null");
            }
            catch
            {
                // Si no está en formato estándar, convertirlo
                standardItem = ConvertToStandardFormat(item);
            }

            // Actualizar exportedAt
            standardItem.ExportedAt = DateTime.UtcNow;

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var jsonBytes = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(standardItem, jsonOptions)
            );

            return File(
                jsonBytes,
                "application/json",
                $"news-item-{id}.json"
            );
        }

        /// <summary>
        /// Importa un archivo JSON en formato estándar acordado (HU-26, HU-27, HU-28)
        /// </summary>
        /// <param name="file">Archivo JSON en formato edu.univ.ingest.v1</param>
        /// <returns>El SourceItem creado</returns>
        [HttpPost("import")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ImportItem(IFormFile file)
        {
            // Validaciones básicas del archivo
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No se proporcionó ningún archivo" });
            }

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "El archivo debe ser un JSON (.json)" });
            }

            if (file.Length > 10 * 1024 * 1024) // 10MB
            {
                return BadRequest(new { message = "El archivo excede el tamaño máximo de 10MB" });
            }

            // Leer el contenido del archivo
            string jsonContent;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                jsonContent = await reader.ReadToEndAsync();
            }

            // Deserializar el JSON
            StandardNewsItemDto? standardItem;
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                standardItem = JsonSerializer.Deserialize<StandardNewsItemDto>(jsonContent, options);
            }
            catch (JsonException ex)
            {
                return BadRequest(new
                {
                    message = "El archivo JSON no es válido",
                    error = ex.Message
                });
            }

            if (standardItem == null)
            {
                return BadRequest(new { message = "El JSON deserializado es null" });
            }

            // HU-27: Validar el formato estándar
            var validationErrors = ValidateStandardFormat(standardItem);
            if (validationErrors.Any())
            {
                return BadRequest(new
                {
                    message = "El JSON no cumple con el formato estándar edu.univ.ingest.v1",
                    errors = validationErrors
                });
            }

            // HU-28: Crear o buscar la Source automáticamente
            var source = await GetOrCreateSource(standardItem.Source);

            // Serializar el item completo para almacenarlo
            var itemJson = JsonSerializer.Serialize(standardItem, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            // Crear el SourceItem
            var sourceItem = new SourceItem
            {
                SourceId = source.Id,
                Json = itemJson,
                CreatedAt = DateTime.UtcNow
            };

            _context.SourceItems.Add(sourceItem);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                "GetSourceItem",
                "SourceItemsApi",
                new { id = sourceItem.Id },
                new
                {
                    message = "Item importado exitosamente",
                    id = sourceItem.Id,
                    sourceId = sourceItem.SourceId,
                    sourceName = source.Name,
                    title = standardItem.Normalized.Title,
                    schemaVersion = standardItem.SchemaVersion
                }
            );
        }

        /// <summary>
        /// Valida que el JSON cumpla con el formato estándar acordado
        /// </summary>
        private List<string> ValidateStandardFormat(StandardNewsItemDto item)
        {
            var errors = new List<string>();

            // Validar schemaVersion
            if (string.IsNullOrWhiteSpace(item.SchemaVersion))
            {
                errors.Add("El campo 'schemaVersion' es obligatorio");
            }
            else if (item.SchemaVersion != "edu.univ.ingest.v1")
            {
                errors.Add($"schemaVersion debe ser 'edu.univ.ingest.v1', se recibió '{item.SchemaVersion}'");
            }

            // Validar source
            if (item.Source == null)
            {
                errors.Add("El campo 'source' es obligatorio");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(item.Source.Name))
                    errors.Add("El campo 'source.name' es obligatorio");

                if (string.IsNullOrWhiteSpace(item.Source.Type))
                    errors.Add("El campo 'source.type' es obligatorio");

                if (string.IsNullOrWhiteSpace(item.Source.Url))
                    errors.Add("El campo 'source.url' es obligatorio");
            }

            // Validar normalized
            if (item.Normalized == null)
            {
                errors.Add("El campo 'normalized' es obligatorio");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(item.Normalized.Title))
                    errors.Add("El campo 'normalized.title' es obligatorio");

                if (string.IsNullOrWhiteSpace(item.Normalized.Content))
                    errors.Add("El campo 'normalized.content' es obligatorio");

                if (item.Normalized.PublishedAt == default)
                    errors.Add("El campo 'normalized.publishedAt' es obligatorio y debe ser una fecha válida");
            }

            return errors;
        }

        /// <summary>
        /// Obtiene una Source existente o crea una nueva basada en SourceDto
        /// </summary>
        private async Task<Source> GetOrCreateSource(SourceDto sourceDto)
        {
            // Buscar por URL (es única)
            var existingSource = await _context.Sources
                .FirstOrDefaultAsync(s => s.Url == sourceDto.Url);

            if (existingSource != null)
            {
                return existingSource;
            }

            // Crear nueva Source
            var newSource = new Source
            {
                Url = sourceDto.Url,
                Name = sourceDto.Name,
                Description = $"Fuente importada automáticamente desde JSON estándar",
                ComponentType = sourceDto.Type,
                RequiresSecret = sourceDto.RequiresSecret
            };

            _context.Sources.Add(newSource);
            await _context.SaveChangesAsync();

            return newSource;
        }

        /// <summary>
        /// Convierte un SourceItem antiguo al formato estándar
        /// </summary>
        private StandardNewsItemDto ConvertToStandardFormat(SourceItem item)
        {
            return new StandardNewsItemDto
            {
                SchemaVersion = "edu.univ.ingest.v1",
                ExportedAt = DateTime.UtcNow,
                Source = new SourceDto
                {
                    Id = item.Source?.Id.ToString(),
                    Name = item.Source?.Name ?? "Unknown",
                    Type = item.Source?.ComponentType ?? "unknown",
                    Url = item.Source?.Url ?? "https://unknown.com",
                    RequiresSecret = item.Source?.RequiresSecret ?? false
                },
                Normalized = new NormalizedContentDto
                {
                    Id = item.Id.ToString(),
                    ExternalId = null,
                    Title = item.Source?.Name ?? "Sin título",
                    Content = item.Json,
                    Summary = "Contenido convertido desde formato antiguo",
                    PublishedAt = item.CreatedAt,
                    Url = item.Source?.Url,
                    Author = null,
                    Language = "es",
                    Category = null
                },
                Raw = new RawDataDto
                {
                    Format = "json",
                    Data = new { original = item.Json }
                }
            };
        }
    }
}