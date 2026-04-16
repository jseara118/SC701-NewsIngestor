using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;
using SC701.Models.DTOs;
using SC701.NewsIngestor.Services.Ingestion;

namespace SC701.NewsIngestor.Controllers
{
    [Authorize]
    public class ItemsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISourceIngestionService _ingestion;

        public ItemsController(AppDbContext context, ISourceIngestionService ingestion)
        {
            _context = context;
            _ingestion = ingestion;
        }

        // HU-21: Si hay items en BD → mostrar desde BD, si NO → mostrar desde fuentes
        public async Task<IActionResult> Index()
        {
            var itemsInDb = await _context.SourceItems
                .Include(i => i.Source)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            if (itemsInDb.Any())
            {
                ViewBag.Source = "database";
                return View(itemsInDb);
            }

            try
            {
                var sources = await _context.Sources.ToListAsync();
                var allItems = new List<StandardNewsItemDto>();

                foreach (var source in sources)
                {
                    var items = await _ingestion.IngestManyAsync(source);
                    allItems.AddRange(items);
                }

                var viewModel = allItems.Select(item => new SourceItemViewModel
                {
                    Id = 0,
                    SourceName = item.Source?.Name ?? "Fuente desconocida",
                    SourceId = int.TryParse(item.Source?.Id, out var sourceId) ? sourceId : 0,
                    ComponentType = item.Source?.Type ?? "unknown",
                    Title = item.Normalized?.Title ?? "Sin título",
                    Summary = item.Normalized?.Summary,
                    PublishedAt = item.Normalized?.PublishedAt ?? DateTime.UtcNow,
                    CreatedAt = item.ExportedAt,
                    IsFromSource = true,
                    StandardItem = item
                }).ToList();

                ViewBag.Source = "sources";
                ViewBag.Message = "No hay items guardados. Mostrando items desde fuentes configuradas.";
                return View("IndexFromSources", viewModel);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Error al leer desde fuentes: {ex.Message}";
                ViewBag.Source = "database";
                return View(itemsInDb);
            }
        }

        // HU-21: Detalles — busca en BD, sin fallback a fuentes (evita llamadas costosas)
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var sourceItem = await _context.SourceItems
                .Include(i => i.Source)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sourceItem == null)
                return NotFound();

            ViewBag.IsFromSource = false;
            return View(sourceItem);
        }

        // HU-26: Subir archivos JSON
        public IActionResult Upload()
        {
            return View();
        }
    }
}