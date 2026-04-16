using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Architecture.Services;
using SC701.Data;
using SC701.Models;

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
            // 1. Noticias GUARDADAS en Base de Datos (últimas 6)
            var savedItems = await _context.SourceItems
                .Include(i => i.Source)
                .OrderByDescending(i => i.CreatedAt)
                .Take(6)
                .ToListAsync();

            // 2. Fuentes para la sección de noticias en vivo
            var sources = await _context.Sources
                .OrderBy(s => s.Name)
                .ToListAsync();
            ViewBag.Sources = sources;

            // 3. Noticias en vivo desde fuentes externas (preview)
            try
            {
                var liveItems = await _sourceReaderService.ReadFromAllSourcesAsync();
                ViewBag.LiveItems = liveItems.Take(6).ToList();
            }
            catch
            {
                ViewBag.LiveItems = null;
            }

            return View(savedItems);
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}