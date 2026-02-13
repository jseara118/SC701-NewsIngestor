using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SC701.Models;
using SC701.Models.ViewModels;
using System.Security.Claims;

namespace SC701.NewsIngestor.Controllers
{
    /// <summary>
    /// Controller para autenticación de usuarios
    /// HU-09: Login/Password
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET: Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "Email o contraseña incorrectos.");
                return View(model);
            }

            var now = DateTime.UtcNow;

            // 🔐 BLOQUEAR si ya hay sesión activa (<20 min)
            var hasActiveSession =
                !string.IsNullOrWhiteSpace(user.CurrentSessionId) &&
                user.LastActivityAt != null &&
                (now - user.LastActivityAt.Value) < TimeSpan.FromMinutes(20);

            if (hasActiveSession)
            {
                ModelState.AddModelError("", "Este usuario ya tiene una sesión activa en otro navegador o dispositivo.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                user.LastLoginAt = now;
                user.CurrentSessionId = Guid.NewGuid().ToString();
                user.LastActivityAt = now;

                await _userManager.UpdateAsync(user);

                // 🔥 Re-emit cookie con claim sid
                await _signInManager.SignOutAsync();

                var claims = new List<Claim>
                {
                    new Claim("sid", user.CurrentSessionId)
                };

                await _signInManager.SignInWithClaimsAsync(user, model.RememberMe, claims);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "La cuenta está bloqueada. Intenta más tarde.");
            }
            else
            {
                ModelState.AddModelError("", "Email o contraseña incorrectos.");
            }

            return View(model);
        }

        // GET: Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                RegisteredAt = DateTime.UtcNow,
                CurrentSessionId = Guid.NewGuid().ToString(),
                LastActivityAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");

                var claims = new List<Claim>
                {
                    new Claim("sid", user.CurrentSessionId!)
                };

                await _signInManager.SignInWithClaimsAsync(user, false, claims);

                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                user.CurrentSessionId = null;
                user.LastActivityAt = null;
                await _userManager.UpdateAsync(user);
            }

            await _signInManager.SignOutAsync();

            return RedirectToAction("Login", "Account");
        }

        // GET: Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
