using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;

namespace SC701.NewsIngestor.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SecretsController : Controller
    {
        private readonly AppDbContext _context;

        public SecretsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Secrets
        public async Task<IActionResult> Index()
        {
            var secrets = await _context.Secrets
                .AsNoTracking()
                .Include(s => s.Source)
                .OrderBy(s => s.SourceId)
                .ThenBy(s => s.Name)
                .ToListAsync();

            return View(secrets);
        }

        // GET: /Secrets/Create
        public async Task<IActionResult> Create()
        {
            await LoadSourcesDropdownAsync();
            return View(new Secret());
        }

        // POST: /Secrets/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SourceId,Name,Value,Description")] Secret secret)
        {
            // Estos campos los setea el código, no el form
            ModelState.Remove("Source");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");

            if (!ModelState.IsValid)
            {
                await LoadSourcesDropdownAsync(secret.SourceId);
                return View(secret);
            }

            secret.CreatedAt = DateTime.UtcNow;
            secret.UpdatedAt = DateTime.UtcNow;

            _context.Secrets.Add(secret);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ Secret creado.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Secrets/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var secret = await _context.Secrets.FindAsync(id);
            if (secret == null) return NotFound();

            secret.Value = string.Empty;

            await LoadSourcesDropdownAsync(secret.SourceId);
            return View(secret);
        }

        // POST: /Secrets/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,SourceId,Name,Value,Description")] Secret secret)
        {
            if (id != secret.Id) return BadRequest();

            ModelState.Remove("Source");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedAt");
            // Value es opcional en Edit (no cambiar si viene vacío)
            if (string.IsNullOrWhiteSpace(secret.Value))
                ModelState.Remove("Value");

            if (!ModelState.IsValid)
            {
                await LoadSourcesDropdownAsync(secret.SourceId);
                return View(secret);
            }

            var dbSecret = await _context.Secrets.FirstOrDefaultAsync(s => s.Id == id);
            if (dbSecret == null) return NotFound();

            dbSecret.SourceId = secret.SourceId;
            dbSecret.Name = secret.Name;
            dbSecret.Description = secret.Description;

            if (!string.IsNullOrWhiteSpace(secret.Value))
                dbSecret.Value = secret.Value;

            dbSecret.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ Secret actualizado.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Secrets/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var secret = await _context.Secrets.FindAsync(id);
            if (secret == null) return NotFound();

            _context.Secrets.Remove(secret);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "🗑️ Secret eliminado.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadSourcesDropdownAsync(int? selectedId = null)
        {
            var sources = await _context.Sources
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewBag.Sources = new SelectList(sources, "Id", "Name", selectedId);
        }
    }
}
