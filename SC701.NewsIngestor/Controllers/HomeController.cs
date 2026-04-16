using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.NewsIngestor.Services.Ingestion;

namespace SC701.NewsIngestor.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISourceIngestionService _ingestion;

        public HomeController(AppDbContext context, ISourceIngestionService ingestion)
        {
            _context = context;
            _ingestion = ingestion;
        }

        public async Task<IActionResult> Index()
        {
            var itemsCount = await _context.SourceItems.CountAsync();
            ViewBag.ItemsCount = itemsCount;
            ViewBag.HasItems = itemsCount > 0;

            if (itemsCount == 0)
            {
                try
                {
                    var sources = await _context.Sources.ToListAsync();
                    ViewBag.SourcesCount = sources.Count;

                    if (sources.Count > 0)
                    {
                        var allItems = new List<SC701.Models.DTOs.StandardNewsItemDto>();
                        foreach (var source in sources.Take(2)) // máx 2 fuentes para no tardar
                        {
                            var items = await _ingestion.IngestManyAsync(source);
                            allItems.AddRange(items);
                        }
                        ViewBag.AvailableItemsFromSources = allItems.Count;
                        ViewBag.SampleItems = allItems.Take(3).ToList();
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