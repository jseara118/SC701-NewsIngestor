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

        // GET: Items
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
                var normalizedItems = await _sourceReaderService.ReadFromAllSourcesAsync();

                var viewModel = normalizedItems.Select(item => new SourceItemViewModel
                {
                    Id = 0,
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

        // GET: Items/Details/5
        public async Task<IActionResult> Details(int? id, string? normalizedId)
        {
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
                catch { }
            }

            if (id == null) return NotFound();

            var sourceItem = await _context.SourceItems
                .Include(i => i.Source)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sourceItem == null) return NotFound();

            ViewBag.IsFromSource = false;
            return View(sourceItem);
        }

        // GET: Items/Upload
        public IActionResult Upload()
        {
            return View();
        }

        // POST: Items/Delete/5
        // HU-11: Solo Admin puede eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.SourceItems.FindAsync(id);

            if (item == null)
            {
                TempData["ErrorMessage"] = "No se encontró la noticia.";
                return RedirectToAction(nameof(Index));
            }

            _context.SourceItems.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Noticia eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
