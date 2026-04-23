using Microsoft.AspNetCore.Authorization;
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
        [HttpGet("export/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ExportItem(int id)
        {
            var item = await _context.SourceItems
                .Include(i => i.Source)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
                return NotFound(new { message = $"No se encontró el item con ID {id}" });

            StandardNewsItemDto standardItem;
            try
            {
                standardItem = JsonSerializer.Deserialize<StandardNewsItemDto>(
                    item.Json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? throw new Exception("Deserialización retornó null");
            }
            catch
            {
                standardItem = ConvertToStandardFormat(item);
            }

            standardItem.ExportedAt = DateTime.UtcNow;

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(standardItem, jsonOptions));
            return File(jsonBytes, "application/json", $"news-item-{id}.json");
        }

        /// <summary>
        /// Importa un archivo JSON en formato estándar acordado (HU-26, HU-27, HU-28, HU-24)
        /// Modo flexible: acepta JSONs incompletos, rellena campos faltantes y avisa al usuario.
        /// </summary>
        [HttpPost("import")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ImportItem(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No se proporcionó ningún archivo" });

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "El archivo debe ser un JSON (.json)" });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "El archivo excede el tamaño máximo de 10MB" });

            string jsonContent;
            using (var reader = new StreamReader(file.OpenReadStream()))
                jsonContent = await reader.ReadToEndAsync();

            StandardNewsItemDto? standardItem;
            try
            {
                standardItem = JsonSerializer.Deserialize<StandardNewsItemDto>(
                    jsonContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch (JsonException ex)
            {
                return BadRequest(new { message = "El archivo JSON tiene sintaxis inválida", error = ex.Message });
            }

            if (standardItem == null)
                return BadRequest(new { message = "El JSON deserializado es null" });

            // ── HU-27: Validación flexible ──────────────────────────────────────
            // Campos bloqueantes (sin estos no podemos hacer nada útil):
            var blockingErrors = ValidateBlockingFields(standardItem);
            if (blockingErrors.Any())
                return BadRequest(new
                {
                    message = "El JSON no puede importarse, faltan campos estructurales mínimos",
                    errors = blockingErrors
                });

            // Campos que se pueden rellenar automáticamente:
            var warnings = new List<string>();
            AutoFillMissingFields(standardItem, warnings);

            // ── HU-28: Crear o buscar la Source automáticamente ─────────────────
            var source = await GetOrCreateSource(standardItem.Source);

            // ── HU-24: Verificar duplicados ─────────────────────────────────────
            var normalizedId = standardItem.Normalized?.Id ?? standardItem.Normalized?.ExternalId;

            if (!string.IsNullOrWhiteSpace(normalizedId))
            {
                var existingItem = await _context.SourceItems
                    .FirstOrDefaultAsync(si => si.NormalizedId == normalizedId);

                if (existingItem != null)
                    return BadRequest(new
                    {
                        message = "Ya existe un item con el mismo ID normalizado",
                        normalizedId = normalizedId,
                        existingItemId = existingItem.Id
                    });
            }

            // Verificar duplicado por título si no hay NormalizedId
            if (string.IsNullOrWhiteSpace(normalizedId) && !string.IsNullOrWhiteSpace(standardItem.Normalized?.Title))
            {
                var title = standardItem.Normalized.Title;
                var existsByTitle = await _context.SourceItems
                    .Where(i => i.SourceId == source.Id)
                    .AnyAsync(i => i.Json.Contains(title));
                if (existsByTitle)
                    return BadRequest(new { message = $"Ya existe una noticia con el título \"{title}\"" });
            }

            // ── Serializar y guardar ─────────────────────────────────────────────
            var itemJson = JsonSerializer.Serialize(standardItem, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var sourceItem = new SourceItem
            {
                SourceId = source.Id,
                Json = itemJson,
                NormalizedId = normalizedId,
                CreatedAt = DateTime.UtcNow
            };

            _context.SourceItems.Add(sourceItem);
            await _context.SaveChangesAsync();

            // 201 con advertencias si hubo campos rellenados automáticamente
            return CreatedAtAction(
                "GetSourceItem",
                "SourceItemsApi",
                new { id = sourceItem.Id },
                new
                {
                    message = warnings.Any()
                        ? "Item importado con campos incompletos que fueron rellenados automáticamente"
                        : "Item importado exitosamente",
                    id = sourceItem.Id,
                    sourceId = sourceItem.SourceId,
                    sourceName = source.Name,
                    title = standardItem.Normalized!.Title,
                    schemaVersion = standardItem.SchemaVersion,
                    warnings = warnings.Any() ? warnings : null   // null si no hay, omitido en JSON
                }
            );
        }

        // ── VALIDACIÓN BLOQUEANTE ────────────────────────────────────────────────
        // Solo falla si no hay forma de recuperarse (estructura mínima rota)
        private static List<string> ValidateBlockingFields(StandardNewsItemDto item)
        {
            var errors = new List<string>();

            // schemaVersion debe existir y ser la correcta
            if (string.IsNullOrWhiteSpace(item.SchemaVersion))
                errors.Add("El campo 'schemaVersion' es obligatorio");
            else if (item.SchemaVersion != "edu.univ.ingest.v1")
                errors.Add($"schemaVersion debe ser 'edu.univ.ingest.v1', se recibió '{item.SchemaVersion}'");

            // source y normalized deben existir como objetos
            if (item.Source == null)
                errors.Add("El bloque 'source' es obligatorio");
            else
            {
                if (string.IsNullOrWhiteSpace(item.Source.Name))
                    errors.Add("El campo 'source.name' es obligatorio");
                if (string.IsNullOrWhiteSpace(item.Source.Url))
                    errors.Add("El campo 'source.url' es obligatorio");
            }

            if (item.Normalized == null)
                errors.Add("El bloque 'normalized' es obligatorio");

            return errors;
        }

        // ── AUTO-FILL DE CAMPOS FALTANTES ────────────────────────────────────────
        // Rellena campos opcionales/incompletos con valores de fallback y registra advertencias
        private static void AutoFillMissingFields(StandardNewsItemDto item, List<string> warnings)
        {
            var n = item.Normalized!;

            // source.type — fallback a "api"
            if (string.IsNullOrWhiteSpace(item.Source.Type))
            {
                item.Source.Type = "api";
                warnings.Add("'source.type' estaba vacío → se asignó 'api'");
            }

            // normalized.title — intentar extraer del raw si existe
            if (string.IsNullOrWhiteSpace(n.Title))
            {
                var rawTitle = TryExtractFromRaw(item.Raw, "title", "Title", "name", "Name");
                n.Title = rawTitle ?? item.Source.Name ?? "Sin título";
                warnings.Add($"'normalized.title' estaba vacío → se rellenó con: \"{n.Title}\"");
            }

            // normalized.content — intentar extraer del raw (plot, description, content, body)
            if (string.IsNullOrWhiteSpace(n.Content))
            {
                var rawContent = TryExtractFromRaw(item.Raw, "plot", "Plot", "description", "Description", "content", "Content", "body", "Body", "summary", "Summary");
                n.Content = rawContent ?? n.Summary ?? n.Title ?? "Sin contenido";
                warnings.Add($"'normalized.content' estaba vacío → se rellenó con contenido del bloque 'raw'");
            }

            // normalized.summary — usar content truncado si falta
            if (string.IsNullOrWhiteSpace(n.Summary))
            {
                n.Summary = n.Content.Length > 220 ? n.Content[..220] + "..." : n.Content;
                warnings.Add("'normalized.summary' estaba vacío → se generó desde 'content'");
            }

            // normalized.publishedAt — usar fecha actual si falta o es default
            if (n.PublishedAt == default || n.PublishedAt == DateTime.MinValue)
            {
                // Intentar extraer del raw (released, date, year)
                var rawDate = TryExtractFromRaw(item.Raw, "released", "Released", "date", "Date", "publishedAt", "PublishedAt");
                if (rawDate != null && DateTime.TryParse(rawDate, out var parsedDate))
                {
                    n.PublishedAt = parsedDate.ToUniversalTime();
                    warnings.Add($"'normalized.publishedAt' estaba vacío → se extrajo del raw: {n.PublishedAt:yyyy-MM-dd}");
                }
                else
                {
                    n.PublishedAt = DateTime.UtcNow;
                    warnings.Add("'normalized.publishedAt' estaba vacío → se usó la fecha actual");
                }
            }

            // normalized.language — fallback a "es"
            if (string.IsNullOrWhiteSpace(n.Language))
            {
                n.Language = "es";
                warnings.Add("'normalized.language' estaba vacío → se asignó 'es'");
            }
        }

        // ── HELPER: extraer valor string del bloque Raw ──────────────────────────
        private static string? TryExtractFromRaw(RawDataDto? raw, params string[] fieldNames)
        {
            if (raw?.Data == null) return null;

            try
            {
                var dataJson = JsonSerializer.Serialize(raw.Data);
                using var doc = JsonDocument.Parse(dataJson);
                return SearchJsonElement(doc.RootElement, fieldNames);
            }
            catch { return null; }
        }

        private static string? SearchJsonElement(JsonElement element, string[] fieldNames)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var fieldName in fieldNames)
                {
                    if (element.TryGetProperty(fieldName, out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        var val = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
                // Buscar recursivamente en objetos anidados
                foreach (var prop in element.EnumerateObject())
                {
                    var result = SearchJsonElement(prop.Value, fieldNames);
                    if (result != null) return result;
                }
            }
            return null;
        }

        // ── HU-28: Obtener o crear Source ────────────────────────────────────────
        private async Task<Source> GetOrCreateSource(SourceDto sourceDto)
        {
            var existingSource = await _context.Sources
                .FirstOrDefaultAsync(s => s.Url == sourceDto.Url);

            if (existingSource != null) return existingSource;

            var newSource = new Source
            {
                Url = sourceDto.Url,
                Name = sourceDto.Name,
                Description = "Fuente importada automáticamente desde JSON estándar",
                ComponentType = sourceDto.Type,
                RequiresSecret = sourceDto.RequiresSecret
            };

            _context.Sources.Add(newSource);
            await _context.SaveChangesAsync();
            return newSource;
        }

        // ── Convertir SourceItem antiguo al formato estándar ─────────────────────
        private static StandardNewsItemDto ConvertToStandardFormat(SourceItem item)
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
                    Title = item.Source?.Name ?? "Sin título",
                    Content = item.Json,
                    Summary = "Contenido convertido desde formato antiguo",
                    PublishedAt = item.CreatedAt,
                    Url = item.Source?.Url,
                    Language = "es"
                },
                Raw = new RawDataDto { Format = "json", Data = new { original = item.Json } }
            };
        }
    }
}