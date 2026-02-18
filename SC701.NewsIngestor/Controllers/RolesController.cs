using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SC701.Models;

namespace SC701.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RolesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public RolesController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // GET: /Admin
        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.ToList();

            var model = new List<UserRoleRowVm>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                model.Add(new UserRoleRowVm
                {
                    UserId = u.Id,
                    Email = u.Email ?? u.UserName ?? "(sin email)",
                    CurrentRole = roles.FirstOrDefault() ?? "User"
                });
            }

            return View(model);
        }

        // POST: /Admin/UpdateRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(string userId, string role)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
                return BadRequest();

            // Solo permitimos estos dos roles (simple)
            if (role != "Admin" && role != "User")
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Evitar que el admin se quite su propio rol
            var currentUserId = _userManager.GetUserId(User);
            if (!string.IsNullOrWhiteSpace(currentUserId) && currentUserId == userId)
            {
                TempData["ErrorMessage"] = "⚠️ No podés cambiar tu propio rol desde aquí.";
                return RedirectToAction(nameof(Index));
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

            var result = await _userManager.AddToRoleAsync(user, role);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "❌ No se pudo actualizar el rol.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "✅ Rol actualizado.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class UserRoleRowVm
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string CurrentRole { get; set; } = "User";
    }
}
