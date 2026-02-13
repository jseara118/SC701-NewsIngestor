using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SC701.Models
{
    /// <summary>
    /// Usuario de la aplicación extendiendo IdentityUser
    /// HU-09: Login/Password
    /// HU-10: Roles
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        [Display(Name = "Nombre Completo")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Fecha de Registro")]
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Último Acceso")]
        public DateTime? LastLoginAt { get; set; }

        public string? CurrentSessionId { get; set; }
        public DateTime? LastActivityAt { get; set; }
    }
}
