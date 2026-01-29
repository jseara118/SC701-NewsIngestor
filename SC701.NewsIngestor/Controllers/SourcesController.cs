using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Models;
using SC701.Data;

//comment: This controller manages the CRUD operations for Source entities in the application.

namespace SC701.NewsIngestor.Controllers
{
    public class SourcesController : Controller
    {
        private readonly AppDbContext _context;

        public SourcesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Sources
        public async Task<IActionResult> Index()
        {
            var sources = await _context.Sources.ToListAsync();
            return View(sources);
        }

        // GET: Sources/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Sources/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
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
    }
}