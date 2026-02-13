using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Models;
using SC701.Data;

//comment: This controller manages the CRUD operations for Source entities in the application.
// HU-11: Restricciones por rol - Solo Admin puede crear/editar/eliminar fuentes

namespace SC701.NewsIngestor.Controllers
{
    [Authorize] // HU-09: Requiere autenticación para todo el controller
    public class SourcesController : Controller
    {
        private readonly AppDbContext _context;

        public SourcesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Sources (Todos pueden ver)
        public async Task<IActionResult> Index()
        {
            var sources = await _context.Sources
                .Include(s => s.SourceItems)
                .OrderBy(s => s.Name)
                .ToListAsync();
            return View(sources);
        }

        // GET: Sources/Create (Solo Admin - HU-11)
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Sources/Create (Solo Admin - HU-11)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Url,Name,Description,ComponentType,RequiresSecret")] Source source)
        {
            if (ModelState.IsValid)
            {
                _context.Add(source);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Fuente '{source.Name}' creada exitosamente";
                return RedirectToAction(nameof(Index));
            }
            return View(source);
        }

        // POST: Sources/AddItem (Solo Admin puede agregar items - HU-11)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddItem(int sourceId)
        {
            // 1. Verificar que la fuente exista
            var source = await _context.Sources
                .FirstOrDefaultAsync(s => s.Id == sourceId);

            if (source == null)
            {
                TempData["ErrorMessage"] = "La fuente seleccionada no existe.";
                return RedirectToAction(nameof(Index));
            }

            // 2. Definir el JSON (por ahora usamos la URL como contenido base)
            string jsonData = source.Url;

            // 3. Verificar si ya existe un item con la misma información
            bool existeItem = await _context.SourceItems.AnyAsync(i =>
                i.SourceId == sourceId &&
                i.Json == jsonData
            );

            if (existeItem)
            {
                TempData["ErrorMessage"] =
                    $"Ya existe un item registrado para la fuente '{source.Name}'. No se permiten duplicados.";
                return RedirectToAction(nameof(Index));
            }

            // 4. Crear el SourceItem
            var newItem = new SourceItem
            {
                SourceId = source.Id,
                Json = jsonData,
                CreatedAt = DateTime.UtcNow
            };

            _context.SourceItems.Add(newItem);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"El item de la fuente '{source.Name}' fue agregado correctamente.";

            return RedirectToAction(nameof(Index));
        }
    }
}