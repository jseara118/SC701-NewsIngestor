using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SC701.Data;
using SC701.Models;
using SC701.Models.ViewModels;

namespace SC701.NewsIngestor.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly AppDbContext _context;

        public SettingsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Settings
        public async Task<IActionResult> Index()
        {
            var vm = new SettingsViewModel
            {
                Language = await GetSettingValueAsync("ui.language", "es"),
                Theme = await GetSettingValueAsync("ui.theme", "dark")
            };

            return View(vm);
        }

        // POST: /Settings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SettingsViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            await UpsertSettingAsync("ui.language", vm.Language, "Idioma de la interfaz (es/en)");
            await UpsertSettingAsync("ui.theme", vm.Theme, "Tema de la interfaz (dark/light)");

            // Cookies para aplicar sin consultar DB cada request
            Response.Cookies.Append("ui_lang", vm.Language, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                Secure = Request.IsHttps
            });

            Response.Cookies.Append("ui_theme", vm.Theme, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                Secure = Request.IsHttps
            });

            TempData["SuccessMessage"] = "✅ Configuración guardada.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> GetSettingValueAsync(string key, string defaultValue)
        {
            var value = await _context.Settings
                .AsNoTracking()
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private async Task UpsertSettingAsync(string key, string value, string? description = null)
        {
            var setting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
            {
                setting = new Setting
                {
                    Key = key,
                    Value = value,
                    Description = description,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Settings.Add(setting);
            }
            else
            {
                setting.Value = value;
                setting.Description = description ?? setting.Description;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }
}
