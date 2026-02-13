using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SC701.Models
{
    /// <summary>
    /// Almacena API keys y secretos asociados a fuentes
    /// HU-08: Settings/Secrets
    /// </summary>
    public class Secret
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "ID de Fuente")]
        public int SourceId { get; set; }

        [ForeignKey(nameof(SourceId))]
        [Display(Name = "Fuente")]
        public Source Source { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        [Display(Name = "Nombre del Secret")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Valor del Secret")]
        public string Value { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Descripción")]
        public string? Description { get; set; }

        [Display(Name = "Fecha de Creación")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Última Actualización")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
