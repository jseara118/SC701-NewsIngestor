using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;
using SC701.NewsIngestor.Services.Ingestion;

namespace SC701.NewsIngestor.Controllers
{
    [Authorize]
    public class SourcesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISourceIngestionService _ingestion;

        public SourcesController(AppDbContext context, ISourceIngestionService ingestion)
        {
            _context = context;
            _ingestion = ingestion;
        }

        // GET: Sources
        public async Task<IActionResult> Index()
        {
            var sources = await _context.Sources
                .Include(s => s.SourceItems)
                .OrderBy(s => s.Name)
                .ToListAsync();
            return View(sources);
        }

        // GET: Sources/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View();

        // POST: Sources/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(
            [Bind("Url,Name,Description,ComponentType,RequiresSecret")] Source source)
        {
            ModelState.Remove("SourceItems");

            if (!ModelState.IsValid)
                return View(source);

            _context.Add(source);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Fuente '{source.Name}' creada exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Sources/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var source = await _context.Sources.FindAsync(id);
            if (source == null) return NotFound();
            return View(source);
        }

        // POST: Sources/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,Url,Name,Description,ComponentType,RequiresSecret")] Source source)
        {
            if (id != source.Id) return BadRequest();

            ModelState.Remove("SourceItems");

            if (!ModelState.IsValid)
                return View(source);

            var existing = await _context.Sources.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Url = source.Url;
            existing.Name = source.Name;
            existing.Description = source.Description;
            existing.ComponentType = source.ComponentType;
            existing.RequiresSecret = source.RequiresSecret;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Fuente '{source.Name}' actualizada.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Sources/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var source = await _context.Sources
                .Include(s => s.SourceItems)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (source == null) return NotFound();

            if (source.SourceItems?.Any() == true)
            {
                TempData["ErrorMessage"] =
                    $"No se puede eliminar '{source.Name}' porque tiene {source.SourceItems.Count} item(s) guardado(s).";
                return RedirectToAction(nameof(Index));
            }

            _context.Sources.Remove(source);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Fuente '{source.Name}' eliminada.";
            return RedirectToAction(nameof(Index));
        }
    }
}
