using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;
using SC701.Models.DTOs;
using SC701.NewsIngestor.Services.Ingestion;

//comment: This controller manages the CRUD operations for Source entities in the application.
// HU-11: Restricciones por rol - Solo Admin puede crear/editar/eliminar fuentes

namespace SC701.NewsIngestor.Controllers
{
    [Authorize] // HU-09: Requiere autenticación para todo el controller
    public class SourcesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISourceIngestionService _ingestion;

        public SourcesController(AppDbContext context, ISourceIngestionService ingestion)
        {
            _context = context;
            _ingestion = ingestion;
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
        /*   [HttpPost]
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
        */
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Url,Name,Description,ComponentType,RequiresSecret")] Source source)
        {
            // Validación mínima manual adicional (opcional pero más explícita para la HU)
            if (string.IsNullOrWhiteSpace(source.Url) ||
                string.IsNullOrWhiteSpace(source.ComponentType))
            {
                TempData["ErrorMessage"] = "Debe ingresar una URL válida y un tipo de componente.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Los datos ingresados no son válidos.";
                return RedirectToAction(nameof(Index));
            }

            _context.Add(source);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Fuente '{source.Name}' creada exitosamente";

            return RedirectToAction(nameof(Index));
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
            StandardNewsItemDto standard;
            try
            {
                standard = await _ingestion.IngestAsync(source);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error leyendo la fuente '{source.Name}': {ex.Message}";
                return RedirectToAction(nameof(Index));
            }

            var jsonData = System.Text.Json.JsonSerializer.Serialize(
                standard,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = false
                }
            );


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