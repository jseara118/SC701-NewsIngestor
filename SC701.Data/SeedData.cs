using Microsoft.AspNetCore.Identity;
using SC701.Models;

namespace SC701.Data
{
    /// <summary>
    /// Inicializa datos de prueba: roles y usuario admin
    /// HU-10: Roles (Admin, User)
    /// </summary>
    public static class SeedData
    {
        public static async Task Initialize(
            IServiceProvider serviceProvider,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            // HU-10: Crear roles
            string[] roleNames = { "Admin", "User" };

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // HU-10: Crear usuario Admin por defecto
            var adminEmail = "admin@newsingestor.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                var newAdmin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Administrador del Sistema",
                    EmailConfirmed = true,
                    RegisteredAt = DateTime.UtcNow
                };

                var createAdmin = await userManager.CreateAsync(newAdmin, "Admin123!");

                if (createAdmin.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }

            // HU-10: Crear usuario User de prueba
            var userEmail = "user@newsingestor.com";
            var normalUser = await userManager.FindByEmailAsync(userEmail);

            if (normalUser == null)
            {
                var newUser = new ApplicationUser
                {
                    UserName = userEmail,
                    Email = userEmail,
                    FullName = "Usuario de Prueba",
                    EmailConfirmed = true,
                    RegisteredAt = DateTime.UtcNow
                };

                var createUser = await userManager.CreateAsync(newUser, "User123!");

                if (createUser.Succeeded)
                {
                    await userManager.AddToRoleAsync(newUser, "User");
                }
            }
        }
    }
}
