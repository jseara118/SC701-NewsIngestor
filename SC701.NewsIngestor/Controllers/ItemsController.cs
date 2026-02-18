using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Architecture.Services;
using SC701.Data;
using SC701.Models;
using SC701.Models.DTOs;

//comment: This controller manages the display of SourceItems in the application.
// HU-11: Restricciones por rol - Todos pueden ver, solo usuarios autenticados pueden importar
// HU-21: Mostrar items desde fuentes si BD está vacía

namespace SC701.NewsIngestor.Controllers
{
    [Authorize] // HU-09: Requiere autenticación
    public class ItemsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly SourceReaderService _sourceReaderService;

        public ItemsController(AppDbContext context, SourceReaderService sourceReaderService)
        {
            _context = context;
            _sourceReaderService = sourceReaderService;
        }

        // GET: Items (Todos los usuarios autenticados pueden ver)
        // HU-21: Si hay items en BD → mostrar desde BD, si NO → mostrar desde fuentes
        public async Task<IActionResult> Index()
        {
            var itemsInDb = await _context.SourceItems
                .Include(i => i.Source)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            // HU-21: Si hay items guardados, mostrarlos desde BD
            if (itemsInDb.Any())
            {
                ViewBag.Source = "database";
                return View(itemsInDb);
            }

            // HU-21: Si NO hay items, leer desde fuentes
            try
            {
                var normalizedItems = await _sourceReaderService.ReadFromAllSourcesAsync();
                
                // Convertir StandardNewsItemDto a un formato que la vista pueda usar
                var viewModel = normalizedItems.Select(item => new SourceItemViewModel
                {
                    Id = 0, // No está guardado en BD
                    SourceName = item.Source?.Name ?? "Fuente desconocida",
                    SourceId = int.TryParse(item.Source?.Id, out var sourceId) ? sourceId : 0,
                    ComponentType = item.Source?.Type ?? "unknown",
                    Title = item.Normalized?.Title ?? "Sin título",
                    Summary = item.Normalized?.Summary,
                    PublishedAt = item.Normalized?.PublishedAt ?? DateTime.UtcNow,
                    CreatedAt = item.ExportedAt,
                    NormalizedId = item.Normalized?.Id,
                    IsFromSource = true,
                    StandardItem = item
                }).ToList();

                ViewBag.Source = "sources";
                ViewBag.Message = "No hay items guardados en la base de datos. Mostrando items desde fuentes configuradas.";
                return View("IndexFromSources", viewModel);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Error al leer desde fuentes: {ex.Message}";
                ViewBag.Source = "database";
                return View(itemsInDb);
            }
        }

        // GET: Items/Details/5 (Todos los usuarios autenticados pueden ver)
        // HU-21: También puede mostrar detalles de items desde fuentes
        public async Task<IActionResult> Details(int? id, string? normalizedId)
        {
            // Si se proporciona normalizedId, buscar desde fuentes
            if (!string.IsNullOrWhiteSpace(normalizedId))
            {
                try
                {
                    var normalizedItems = await _sourceReaderService.ReadFromAllSourcesAsync();
                    var item = normalizedItems.FirstOrDefault(i => 
                        i.Normalized?.Id == normalizedId || 
                        i.Normalized?.ExternalId == normalizedId);
                    
                    if (item != null)
                    {
                        ViewBag.IsFromSource = true;
                        return View("DetailsFromSource", item);
                    }
                }
                catch
                {
                    // Continuar con búsqueda en BD
                }
            }

            // Búsqueda normal en BD
            if (id == null)
            {
                return NotFound();
            }

            var sourceItem = await _context.SourceItems
                .Include(i => i.Source)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sourceItem == null)
            {
                return NotFound();
            }

            ViewBag.IsFromSource = false;
            return View(sourceItem);
        }

        // GET: Items/Upload (Todos los usuarios autenticados pueden importar)
        // HU-26: Vista para subir archivos JSON
        public IActionResult Upload()
        {
            return View();
        }
    }
}