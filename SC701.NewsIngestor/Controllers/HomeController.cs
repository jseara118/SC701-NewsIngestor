using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Architecture.Services;
using SC701.Data;
using SC701.Models;

//comment: This is a simple HomeController for an ASP.NET Core MVC application.
// HU-21: Mostrar items desde fuentes si BD está vacía

namespace SC701.NewsIngestor.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly SourceReaderService _sourceReaderService;

        public HomeController(AppDbContext context, SourceReaderService sourceReaderService)
        {
            _context = context;
            _sourceReaderService = sourceReaderService;
        }

        public async Task<IActionResult> Index()
        {
            // HU-21: Verificar si hay items en BD para mostrar en la landing page
            var itemsCount = await _context.SourceItems.CountAsync();
            ViewBag.ItemsCount = itemsCount;
            ViewBag.HasItems = itemsCount > 0;

            // Si no hay items, intentar leer desde fuentes para mostrar preview
            if (itemsCount == 0)
            {
                try
                {
                    var sourcesCount = await _context.Sources.CountAsync();
                    ViewBag.SourcesCount = sourcesCount;
                    
                    if (sourcesCount > 0)
                    {
                        var normalizedItems = await _sourceReaderService.ReadFromAllSourcesAsync();
                        ViewBag.AvailableItemsFromSources = normalizedItems.Count;
                        ViewBag.SampleItems = normalizedItems.Take(3).ToList(); // Mostrar 3 items de ejemplo
                    }
                }
                catch
                {
                    // Si hay error, simplemente no mostrar preview
                }
            }

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}