using System.ComponentModel.DataAnnotations;

namespace SC701.Models
{
    /// <summary>
    /// Configuración general de la aplicación
    /// HU-08: Settings/Secrets
    /// </summary>
    public class Setting
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Display(Name = "Clave")]
        public string Key { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Valor")]
        public string Value { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Descripción")]
        public string? Description { get; set; }

        [Display(Name = "Última Actualización")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
