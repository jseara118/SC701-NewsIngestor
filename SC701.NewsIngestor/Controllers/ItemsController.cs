using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Models;
using SC701.Data;

//comment: This controller manages the display of SourceItems in the application.
// HU-11: Restricciones por rol - Todos pueden ver, solo usuarios autenticados pueden importar

namespace SC701.NewsIngestor.Controllers
{
    [Authorize] // HU-09: Requiere autenticación
    public class ItemsController : Controller
    {
        private readonly AppDbContext _context;

        public ItemsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Items (Todos los usuarios autenticados pueden ver)
        public async Task<IActionResult> Index()
        {
            var items = await _context.SourceItems
                .Include(i => i.Source)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
            return View(items);
        }

        // GET: Items/Details/5 (Todos los usuarios autenticados pueden ver)
        public async Task<IActionResult> Details(int? id)
        {
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